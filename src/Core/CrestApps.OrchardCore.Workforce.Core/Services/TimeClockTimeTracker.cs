using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The time clock in Time Tracker: punches stay the clock's record, and each worker with a login has their day kept
/// in Time Tracker too, in the stages of their activities, so it is reported by category with everyone else's time.
/// </summary>
public static class TimeClockTimeTracker
{
    /// <summary>
    /// The id the time clock keeps its entries under, in <see cref="TimeEntry.ManagedBy"/>.
    /// </summary>
    public const string ClockId = "Workforce.TimeClock";
}

/// <summary>
/// Where the time clock's link in Time Tracker goes.
/// </summary>
public sealed class TimeClockTimeTrackerOptions
{
    /// <summary>
    /// Gets or sets where a worker clocks in, such as <c>~/my-work</c>; <see langword="null"/> when they clock in only
    /// at a kiosk.
    /// </summary>
    public string ClockUrl { get; set; }

    /// <summary>
    /// Gets or sets where a manager corrects punches, such as the timesheets.
    /// </summary>
    public string ManageUrl { get; set; }
}

/// <summary>
/// Keeps a worker's day in Time Tracker after every punch, correction and removal.
/// </summary>
internal sealed class TimeClockTimeTrackerSync : ITimePunchObserver
{
    // Resolved when a punch is saved: the day sync reads punches through the clock service that calls this.
    private readonly IServiceProvider _serviceProvider;

    public TimeClockTimeTrackerSync(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task PunchedAsync(TimePunch punch, TimePunch replaced, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(punch);

        var from = replaced is not null && replaced.Utc < punch.Utc ? replaced.Utc : punch.Utc;
        var to = replaced is not null && replaced.Utc > punch.Utc ? replaced.Utc : punch.Utc;

        return _serviceProvider.GetRequiredService<TimeClockDaySync>().SyncAsync(punch.WorkerId, from, to, cancellationToken);
    }
}

/// <summary>
/// Rebuilds a worker's Time Tracker entries around a time from their punches.
/// </summary>
internal sealed class TimeClockDaySync
{
    // How far either side of a change entries are rebuilt, and how much more of the punches is read to rebuild them.
    private static readonly TimeSpan _reach = TimeSpan.FromDays(1);
    private static readonly TimeSpan _readReach = TimeSpan.FromDays(2);

    private readonly WorkerStore _workers;
    private readonly ITimeClockService _timeClock;
    private readonly ITimeTrackerService _tracker;
    private readonly IActivityCodeManager _activities;
    private readonly IClock _clock;

    public TimeClockDaySync(WorkerStore workers, ITimeClockService timeClock, ITimeTrackerService tracker, IActivityCodeManager activities, IClock clock)
    {
        _workers = workers;
        _timeClock = timeClock;
        _tracker = tracker;
        _activities = activities;
        _clock = clock;
    }

    /// <summary>
    /// Rebuilds the entries of a worker from a day before one time to a day after another.
    /// </summary>
    /// <returns>The result, or <see langword="null"/> when the worker has no login to keep time for.</returns>
    public async Task<TimeTrackerResult> SyncAsync(string workerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var worker = await _workers.FindByIdAsync(workerId, cancellationToken);

        if (worker is null || string.IsNullOrEmpty(worker.UserId))
        {
            return null;
        }

        var from = fromUtc - _reach;
        var to = toUtc + _reach;

        return await _tracker.SyncAsync(new TimeEntrySync
        {
            UserId = worker.UserId,
            UserName = worker.DisplayName,
            ManagedBy = TimeClockTimeTracker.ClockId,
            Source = TimeTrackerConstants.Sources.Kiosk,
            FromUtc = from,
            ToUtc = to,
            BuildAsync = async token =>
            {
                var punches = await _timeClock.ListAsync(worker.ItemId, from - _readReach, to + _readReach, token);
                var stages = TimeClockStages.From(await _activities.GetAllAsync(token));

                return Build(punches, stages, _clock.UtcNow);
            },
        }, cancellationToken);
    }

    /// <summary>
    /// Turns punches into entries the way timesheets pair them: from clocking in to clocking out, with meals and
    /// breaks between. Time stays as punched; rounding is for pay. An open stretch left long enough to be a missed
    /// punch is left out, as timesheets leave it out.
    /// </summary>
    internal static List<TimeEntry> Build(IEnumerable<TimePunch> punches, TimeClockStages stages, DateTime nowUtc)
    {
        var byId = punches.ToDictionary(punch => punch.ItemId, StringComparer.Ordinal);
        var entries = new List<TimeEntry>();

        foreach (var stretch in DefaultTimesheetCalculator.Pair(byId.Values))
        {
            foreach (var span in stretch.Spans)
            {
                if (span.End is null && nowUtc - stretch.Start > DefaultTimesheetCalculator.MissedWithoutShift)
                {
                    continue;
                }

                if (span.End is { } end && end <= span.Start)
                {
                    continue;
                }

                var stageId = stages.For(span.Kind, span.ActivityCodeId);

                if (stageId is null)
                {
                    continue;
                }

                entries.Add(new TimeEntry
                {
                    StageId = stageId,
                    StartUtc = span.Start,
                    EndUtc = span.End,
                    Source = byId.TryGetValue(span.StartPunchId ?? string.Empty, out var punch) ? Source(punch.Source) : null,
                    SourceRef = span.StartPunchId,
                    Note = span.Job,
                });
            }
        }

        return entries;
    }

    private static string Source(TimePunchSource source) => source switch
    {
        TimePunchSource.Web => TimeTrackerConstants.Sources.Self,
        TimePunchSource.Mobile => TimeTrackerConstants.Sources.Mobile,
        TimePunchSource.Kiosk => TimeTrackerConstants.Sources.Kiosk,
        TimePunchSource.ContactCenter => TimeTrackerConstants.Sources.ContactCenter,
        TimePunchSource.Manager => TimeTrackerConstants.Sources.Manager,
        _ => TimeTrackerConstants.Sources.Api,
    };
}

/// <summary>
/// The stage time on the clock is kept in: an activity's own stage; for a meal or a break, the stage of the first
/// meal or break activity; otherwise the stage of the first work activity.
/// </summary>
internal sealed class TimeClockStages
{
    private readonly Dictionary<string, string> _byActivity;
    private readonly string _work;
    private readonly string _meal;
    private readonly string _break;

    private TimeClockStages(Dictionary<string, string> byActivity, string work, string meal, string breakStage)
    {
        _byActivity = byActivity;
        _work = work;
        _meal = meal;
        _break = breakStage;
    }

    public static TimeClockStages From(IEnumerable<ActivityCode> codes)
    {
        var withStages = codes.Where(code => !string.IsNullOrEmpty(code.StageId)).ToList();

        string First(ActivityKind kind) => withStages.Where(code => code.Kind == kind).OrderBy(code => code.Enabled ? 0 : 1).Select(code => code.StageId).FirstOrDefault();

        return new TimeClockStages(
            withStages.ToDictionary(code => code.ItemId, code => code.StageId, StringComparer.Ordinal),
            First(ActivityKind.Work),
            First(ActivityKind.Meal),
            First(ActivityKind.Break));
    }

    public string For(TimesheetSpanKind kind, string activityCodeId) => kind switch
    {
        TimesheetSpanKind.Meal => _meal,
        TimesheetSpanKind.Break => _break,
        _ => activityCodeId is not null && _byActivity.TryGetValue(activityCodeId, out var stageId) ? stageId : _work,
    };
}

/// <summary>
/// Gives every activity code a Time Tracker stage: one with its code or name, or a new one counting toward
/// <c>Paid</c> when it is paid and <c>Worked</c> when it counts as work.
/// </summary>
internal sealed class TimeClockStageSeeder
{
    private readonly IActivityCodeManager _activities;
    private readonly TimeClockStageAttacher _attacher;

    public TimeClockStageSeeder(IActivityCodeManager activities, TimeClockStageAttacher attacher)
    {
        _activities = activities;
        _attacher = attacher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var code in await _activities.GetAllAsync(cancellationToken))
        {
            if (!string.IsNullOrEmpty(code.StageId))
            {
                continue;
            }

            await _attacher.AttachAsync(code, cancellationToken);
            await _activities.UpdateAsync(code, cancellationToken: cancellationToken);
        }
    }
}

/// <summary>
/// Finds or makes the stage of an activity code.
/// </summary>
internal sealed class TimeClockStageAttacher
{
    private readonly ITimeStageManager _stages;
    private readonly ITimeCategoryManager _categories;

    public TimeClockStageAttacher(ITimeStageManager stages, ITimeCategoryManager categories)
    {
        _stages = stages;
        _categories = categories;
    }

    /// <summary>
    /// Sets an activity code's stage, making the stage when there is none to share.
    /// </summary>
    public async Task AttachAsync(ActivityCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        var stages = (await _stages.GetAllAsync(cancellationToken)).ToList();
        var stage = stages.FirstOrDefault(stage => !string.IsNullOrEmpty(code.Code) && string.Equals(stage.Code, code.Code, StringComparison.OrdinalIgnoreCase))
            ?? stages.FirstOrDefault(stage => string.Equals(stage.Name, code.Name, StringComparison.OrdinalIgnoreCase));

        if (stage is null)
        {
            stage = await _stages.NewAsync(cancellationToken: cancellationToken);
            stage.Name = code.Name;
            stage.Code = code.Code;
            stage.Color = code.Color;
            stage.Position = stages.Count * 10;
            stage.SelfService = false;
            stage.Enabled = code.Enabled;

            if (code.IsPaid)
            {
                stage.CategoryIds.Add((await CategoryAsync("Paid", "PAID", "#2b8a3e", 10, cancellationToken)).ItemId);
            }

            if (code.CountsAsWork)
            {
                stage.CategoryIds.Add((await CategoryAsync("Worked", "WORKED", "#1971c2", 20, cancellationToken)).ItemId);
            }

            await _stages.CreateAsync(stage, cancellationToken: cancellationToken);
        }

        code.StageId = stage.ItemId;
    }

    private async Task<TimeCategory> CategoryAsync(string name, string code, string color, int position, CancellationToken cancellationToken)
    {
        var found = (await _categories.GetAllAsync(cancellationToken)).FirstOrDefault(category => string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(category.Code, code, StringComparison.OrdinalIgnoreCase));

        if (found is not null)
        {
            return found;
        }

        var category = await _categories.NewAsync(cancellationToken: cancellationToken);

        category.Name = name;
        category.Code = code;
        category.Color = color;
        category.Position = position;
        await _categories.CreateAsync(category, cancellationToken: cancellationToken);

        return category;
    }
}

/// <summary>
/// Gives a new activity code its stage when it has none.
/// </summary>
internal sealed class ActivityCodeStageHandler : CatalogEntryHandlerBase<ActivityCode>
{
    private readonly TimeClockStageAttacher _attacher;

    public ActivityCodeStageHandler(TimeClockStageAttacher attacher)
    {
        _attacher = attacher;
    }

    public override async Task CreatingAsync(CreatingContext<ActivityCode> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Model.StageId))
        {
            await _attacher.AttachAsync(context.Model, cancellationToken);
        }
    }
}

/// <summary>
/// The time clock, as Time Tracker sees it: it keeps the time of every worker with a login.
/// </summary>
internal sealed class TimeClockExternalClock : IExternalTimeClock
{
    private readonly WorkerStore _workers;
    private readonly TimeClockTimeTrackerOptions _options;

    internal readonly IStringLocalizer S;

    public TimeClockExternalClock(WorkerStore workers, IOptions<TimeClockTimeTrackerOptions> options, IStringLocalizer<TimeClockExternalClock> stringLocalizer)
    {
        _workers = workers;
        _options = options.Value;
        S = stringLocalizer;
    }

    public async Task<ExternalTimeClockInfo> FindAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var worker = await _workers.FindByUserIdAsync(userId, cancellationToken);

        return worker is null || worker.Status == WorkerStatus.Terminated ? null : Info();
    }

    public Task<ExternalTimeClockInfo> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(id == TimeClockTimeTracker.ClockId ? Info() : null);

    private ExternalTimeClockInfo Info() => new()
    {
        Id = TimeClockTimeTracker.ClockId,
        Name = S["the Time Clock"],
        Url = _options.ClockUrl,
        ManageUrl = _options.ManageUrl,
    };
}

/// <summary>
/// Brings punches made before the time clock kept time in Time Tracker in, a batch of workers at a time, once.
/// </summary>
internal sealed class TimeClockTrackerBackfill
{
    private static readonly DateTime _beginning = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISession _session;
    private readonly ITimeClockService _timeClock;
    private readonly TimeClockDaySync _daySync;
    private readonly IClock _clock;

    public TimeClockTrackerBackfill(ISession session, ITimeClockService timeClock, TimeClockDaySync daySync, IClock clock)
    {
        _session = session;
        _timeClock = timeClock;
        _daySync = daySync;
        _clock = clock;
    }

    /// <summary>
    /// Brings in the next workers' punches.
    /// </summary>
    /// <param name="batchSize">How many workers.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many workers had their time brought in.</returns>
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var state = await _session.Query<TimeClockTrackerBackfillState>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new TimeClockTrackerBackfillState();

        if (state.Completed)
        {
            return 0;
        }

        var workers = await _session.Query<WorkerProfile, WorkerIndex>(index => index.UserId != null, collection: WorkforceConstants.CollectionName)
            .OrderBy(index => index.ItemId)
            .Skip(state.Done)
            .Take(batchSize)
            .ListAsync(cancellationToken);
        var now = _clock.UtcNow;
        var synced = 0;

        foreach (var worker in workers)
        {
            var punches = await _timeClock.ListAsync(worker.ItemId, _beginning, now, cancellationToken);

            if (punches.Count > 0 && await _daySync.SyncAsync(worker.ItemId, punches[0].Utc, now, cancellationToken) is { Succeeded: true })
            {
                synced++;
            }
        }

        var count = workers.Count;

        state.Done += count;
        state.Completed = count < batchSize;
        await _session.SaveAsync(state, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        return synced;
    }
}

/// <summary>
/// How far the backfill has gone.
/// </summary>
public sealed class TimeClockTrackerBackfillState
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets how many workers it went through.
    /// </summary>
    public int Done { get; set; }

    /// <summary>
    /// Gets or sets whether it went through them all.
    /// </summary>
    public bool Completed { get; set; }
}
