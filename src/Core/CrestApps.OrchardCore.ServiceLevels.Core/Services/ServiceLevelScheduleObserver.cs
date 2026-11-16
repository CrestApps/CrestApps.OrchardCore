using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Keeps the running timers right when the hours of their schedule change or the schedule is deleted. Before the change,
/// while reads still return the old hours, it keeps a copy of them and the time of the change, and asks for the
/// <see cref="ServiceLevelScheduleChangesStep"/>. That step, in the background and a batch at a time, counts each running
/// timer's time up to the change on the old hours and works out its due and warning times from the change on the new
/// ones. Nothing is done in the admin's request beyond keeping the copy.
/// </summary>
public sealed class ServiceLevelScheduleObserver : IBusinessHoursScheduleObserver
{
    private readonly IEnumerable<IHoursScheduleSource> _sources;
    private readonly ResumableSteps _steps;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelScheduleObserver"/> class.
    /// </summary>
    /// <param name="sources">The sources of schedules, to read the hours before they change.</param>
    /// <param name="steps">The resumable steps.</param>
    /// <param name="clock">The clock.</param>
    public ServiceLevelScheduleObserver(IEnumerable<IHoursScheduleSource> sources, ResumableSteps steps, IClock clock)
    {
        _sources = sources;
        _steps = steps;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task ChangingAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scheduleId))
        {
            return;
        }

        HoursSchedule hours = null;

        foreach (var source in _sources)
        {
            if ((hours = await source.FindAsync(scheduleId, cancellationToken)) is not null)
            {
                break;
            }
        }

        var change = new ServiceLevelScheduleChange { ScheduleId = scheduleId, AtUtc = _clock.UtcNow, OldHours = hours };

        await _steps.RequestAsync(ServiceLevelScheduleChangesStep.StepName, new Dictionary<string, string>
        {
            [change.Key] = JsonSerializer.Serialize(change),
        });
    }

    /// <inheritdoc/>
    public Task ChangedAsync(string scheduleId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// A change of a schedule's hours that its running timers still have to catch up with.
/// </summary>
public sealed class ServiceLevelScheduleChange
{
    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    public string ScheduleId { get; set; }

    /// <summary>
    /// Gets or sets when the hours changed, in UTC.
    /// </summary>
    public DateTime AtUtc { get; set; }

    /// <summary>
    /// Gets or sets the hours before the change, or <see langword="null"/> when the schedule was unknown (around the clock).
    /// </summary>
    public HoursSchedule OldHours { get; set; }

    /// <summary>
    /// Gets the key the change is kept under, in order of time for one schedule.
    /// </summary>
    public string Key => ScheduleId + "|" + AtUtc.Ticks.ToString("D20", CultureInfo.InvariantCulture);
}

/// <summary>
/// Catches the running timers up with the changes of their schedules, a batch at a time, oldest change first.
/// </summary>
public sealed class ServiceLevelScheduleChangesStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "ServiceLevels.ScheduleChanges";

    private const int TimersPerRun = 200;

    private readonly IServiceLevelPolicyManager _policies;
    private readonly IServiceLevelTimerStore _timers;
    private readonly ServiceLevelClock _timerClock;
    private readonly IEnumerable<IServiceLevelObserver> _observers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelScheduleChangesStep"/> class.
    /// </summary>
    /// <param name="policies">The policy manager.</param>
    /// <param name="timers">The timer store.</param>
    /// <param name="timerClock">The service level clock.</param>
    /// <param name="observers">The observers, told when a timer's schedule is no longer ever open.</param>
    public ServiceLevelScheduleChangesStep(IServiceLevelPolicyManager policies, IServiceLevelTimerStore timers, ServiceLevelClock timerClock, IEnumerable<IServiceLevelObserver> observers)
    {
        _policies = policies;
        _timers = timers;
        _timerClock = timerClock;
        _observers = observers;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var budget = TimersPerRun;

        foreach (var key in context.Data.Keys.Order(StringComparer.Ordinal).ToList())
        {
            if (budget <= 0)
            {
                break;
            }

            var change = JsonSerializer.Deserialize<ServiceLevelScheduleChange>(context.Data[key]);
            var allowed = budget;

            // The timers still counting from before the change; a timer caught up counts from the change on.
            var behind = (await RunningTimersAsync(change.ScheduleId, cancellationToken))
                .Where(timer => timer.CountingSinceUtc < change.AtUtc)
                .Take(allowed)
                .ToList();

            foreach (var timer in behind)
            {
                var hadHours = !timer.NoBusinessHours;

                ServiceLevelClock.SettleOn(timer, change.OldHours, change.AtUtc);
                await _timerClock.ScheduleAsync(timer, change.AtUtc, cancellationToken);
                await _timers.UpdateAsync(timer, cancellationToken);

                // A schedule that is no longer ever open: the record's module notes it, once.
                if (hadHours && timer.NoBusinessHours)
                {
                    foreach (var observer in _observers)
                    {
                        await observer.NoBusinessHoursAsync(timer, cancellationToken);
                    }
                }
            }

            budget -= behind.Count;

            // Fewer behind than this run could take: the change is caught up.
            if (behind.Count < allowed)
            {
                context.Data.Remove(key);
            }
        }

        return context.Data.Count == 0;
    }

    private async Task<IReadOnlyList<ServiceLevelTimer>> RunningTimersAsync(string scheduleId, CancellationToken cancellationToken)
    {
        var policyIds = (await _policies.GetAllAsync(cancellationToken))
            .Where(policy => string.Equals(policy.HoursScheduleId, scheduleId, StringComparison.Ordinal))
            .Select(policy => policy.ItemId)
            .ToArray();

        var timers = await _timers.ListRunningByPoliciesAsync(policyIds, cancellationToken);

        // A timer keeps the schedule it started on until its policy is retargeted.
        return [.. timers.Where(timer => string.Equals(timer.HoursScheduleId, scheduleId, StringComparison.Ordinal))];
    }
}
