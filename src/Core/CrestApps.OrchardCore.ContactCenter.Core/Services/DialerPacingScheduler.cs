using System.Collections.Concurrent;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerPacingScheduler"/>.
/// </summary>
/// <remarks>
/// <para>
/// A request arms one deadline per queue (<c>dialer-pacing:{queueId}</c>) a debounce after it, and further requests while
/// it is armed are merged into it rather than pushing it back, so a busy queue is still paced. The run resolves the
/// profile the queue's waiting work was loaded under, takes the queue's pacing lock without waiting, runs the profile's
/// pacing cycle through the dialer and commits before letting the lock go, so the next cycle -- here or on another node
/// -- sees the agents this one reserved.
/// </para>
/// <para>
/// A run that finds the lock held asks to run again shortly: the holder may have measured the queue before the change
/// that asked for this run. While a cycle keeps placing calls the queue is paced again, so agents freed together are each
/// dialed in turn; once a cycle places nothing it waits for the next event or the minute dialer pacing task. Each cycle
/// applies the profile's own limits, so this changes when a campaign is paced, never how hard.
/// </para>
/// <para>
/// The minute dialer pacing task re-arms every campaign through <see cref="RequestBackstop(string)"/>, which does not
/// merge into a pending request. A request is pending from when it is armed until its run starts; if the deadline
/// scheduler drops the armed run before it starts (the shell scope could not be opened, the scheduler was being disposed),
/// the pending mark would otherwise merge every later request into a run that never comes, and the campaign would stop
/// dialing without an error. The backstop replaces the armed run, so that can last a minute at most, and a mark it finds
/// older than any armed run should live is logged as a warning.
/// </para>
/// </remarks>
internal sealed class DialerPacingScheduler : IDialerPacingScheduler
{
    private readonly ConcurrentDictionary<string, DateTime> _pending = new(StringComparer.Ordinal);
    private readonly IContactCenterDeadlineScheduler _deadlineScheduler;
    private readonly IClock _clock;
    private readonly ContactCenterDialerPacingOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingScheduler"/> class.
    /// </summary>
    /// <param name="deadlineScheduler">The in-process deadline scheduler the runs are held on.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The dialer pacing options.</param>
    /// <param name="logger">The logger.</param>
    public DialerPacingScheduler(
        IContactCenterDeadlineScheduler deadlineScheduler,
        IClock clock,
        IOptions<ContactCenterDialerPacingOptions> options,
        ILogger<DialerPacingScheduler> logger)
    {
        _deadlineScheduler = deadlineScheduler;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// The key of the deadline that paces the queue.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    public static string GetDeadlineKey(string queueId)
        => $"dialer-pacing:{queueId}";

    /// <summary>
    /// The key of the lock a run holds while it paces the queue.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    public static string GetPacingLockKey(string queueId)
        => $"ContactCenterDialerPacing:{queueId}";

    /// <summary>
    /// Whether the profile is paced by the reserve-then-dial cycle this scheduler runs. An over-dialing Predictive profile
    /// is paced by <see cref="IPredictivePacingScheduler"/> instead.
    /// </summary>
    /// <param name="profile">The dialer profile.</param>
    public static bool IsPacedHere(DialerProfile profile)
        => profile is not null &&
            profile.Enabled &&
            (profile.Mode is DialerMode.Power or DialerMode.Progressive ||
                (profile.Mode == DialerMode.Predictive && profile.PredictivePacingModel != PredictivePacingModel.OverDial));

    /// <inheritdoc/>
    public void Request(string queueId)
        => Request(queueId, _options.Debounce);

    /// <inheritdoc/>
    public void RequestRetry(string queueId)
        => Request(queueId, _options.LockRetryDelay);

    /// <inheritdoc/>
    public void RequestBackstop(string queueId)
    {
        if (!ContactCenterConstants.IsCampaignQueue(queueId))
        {
            return;
        }

        var now = _clock.UtcNow;

        // An armed run starts within the debounce or the lock retry delay; one pending far longer was dropped before it
        // started, and until now every request for the queue was merged into it.
        if (_pending.TryGetValue(queueId, out var pendingSince) && now - pendingSince > StalePendingAge)
        {
            _logger.LogWarning(
                "The armed pacing run of queue '{QueueId}' never started; it had been pending for {PendingAge}. The minute backstop re-armed it.",
                queueId.SanitizeLogValue(),
                now - pendingSince);
        }

        // Always armed afresh: scheduling under the same key replaces any run already armed, so this never paces the queue
        // twice, and the pacing lock keeps it from overlapping a run already under way.
        _pending[queueId] = now;
        Arm(queueId, now.Add(_options.Debounce));
    }

    /// <summary>
    /// Gets how long a request may stay pending before the backstop treats its run as dropped.
    /// </summary>
    internal TimeSpan StalePendingAge
        => _options.LockExpiration + _options.Debounce;

    private void Request(string queueId, TimeSpan delay)
    {
        if (!ContactCenterConstants.IsCampaignQueue(queueId))
        {
            return;
        }

        var now = _clock.UtcNow;

        // Merged into the run already armed: pushing it back on every event would let a busy queue starve.
        if (!_pending.TryAdd(queueId, now))
        {
            return;
        }

        Arm(queueId, now.Add(delay));
    }

    private void Arm(string queueId, DateTime dueUtc)
        => _deadlineScheduler.Schedule(GetDeadlineKey(queueId), dueUtc, (services, cancellationToken) => RunAsync(services, queueId, cancellationToken));

    /// <summary>
    /// Runs one pacing cycle of the queue on the given scope.
    /// </summary>
    /// <param name="services">The services of the scope the run commits.</param>
    /// <param name="queueId">The campaign queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>When to pace the queue again, or <see langword="null"/> to wait for the next request.</returns>
    internal async Task<DateTime?> RunAsync(IServiceProvider services, string queueId, CancellationToken cancellationToken)
    {
        // A request arriving from here on arms the next run.
        _pending.TryRemove(queueId, out _);

        using var workLease = services.GetRequiredService<IContactCenterFeatureWorkManager>().TryEnter(ContactCenterConstants.Feature.DialerPaced);

        if (workLease is null)
        {
            return null;
        }

        var head = await services.GetRequiredService<IQueueItemStore>().FindNextWaitingAsync(queueId, cancellationToken);

        if (head is null || string.IsNullOrEmpty(head.DialerProfileId))
        {
            return null;
        }

        var profile = await services.GetRequiredService<IDialerProfileReader>().FindByIdAsync(head.DialerProfileId, cancellationToken);

        if (!IsPacedHere(profile))
        {
            return null;
        }

        (var locker, var locked) = await services.GetRequiredService<IDistributedLock>().TryAcquireLockAsync(
            GetPacingLockKey(queueId),
            TimeSpan.Zero,
            _options.LockExpiration);

        if (!locked)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipped a pacing run of queue '{QueueId}' because another run holds its pacing lock; it is paced again shortly.", queueId.SanitizeLogValue());
            }

            RequestRetry(queueId);

            return null;
        }

        await using var acquiredLock = locker;

        var session = services.GetRequiredService<ISession>();
        int started;

        try
        {
            started = await services.GetRequiredService<IDialerService>().RunCycleAsync(profile, queueId, cancellationToken);

            // Committed before the lock is released: the next cycle, here or on another node, reads the agents this one
            // reserved.
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            // Another transition saved an agent or item this cycle reserved first. The cycle's calls were rolled back with
            // it and are never dispatched; the queue is paced again on what the winner left.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("A pacing run of queue '{QueueId}' lost a write race; none of its calls were placed and it is paced again shortly.", queueId.SanitizeLogValue());
            }

            await session.CancelAsync();
            RequestRetry(queueId);

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "A pacing run of queue '{QueueId}' failed; none of its calls were placed. The minute dialer pacing task paces it again.", queueId.SanitizeLogValue());

            await session.CancelAsync();

            return null;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Paced queue '{QueueId}' on request: {Started} call(s) placed.", queueId.SanitizeLogValue(), started);
        }

        return started > 0 ? _clock.UtcNow.Add(_options.Debounce) : null;
    }
}
