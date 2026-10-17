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
    /// <summary>
    /// The longest description of a failure stored with a cycle's record.
    /// </summary>
    private const int MaxDetailLength = 500;

    private readonly ConcurrentDictionary<string, DateTime> _pending = new(StringComparer.Ordinal);

    // Consecutive times a queue's run found its pacing lock held, so a lock that stays held is retried less and less often.
    private readonly ConcurrentDictionary<string, int> _lockMisses = new(StringComparer.Ordinal);
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
    /// <remarks>
    /// Every way the cycle can end is recorded through <see cref="IDialerPacingCycleRecorder"/>, when the scope has one, on
    /// a scope of its own: a cycle that is rolled back, abandoned at its time budget or that throws is on record as such.
    /// </remarks>
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

        var recorder = services.GetService<IDialerPacingCycleRecorder>();
        var startedUtc = _clock.UtcNow;
        var head = await services.GetRequiredService<IQueueItemStore>().FindNextWaitingAsync(queueId, cancellationToken);

        if (head is null)
        {
            await RecordAsync(recorder, queueId, null, startedUtc, DialerPacingCycleOutcome.NothingDue, "No record is waiting in the queue.");

            return null;
        }

        if (string.IsNullOrEmpty(head.DialerProfileId))
        {
            await RecordAsync(recorder, queueId, null, startedUtc, DialerPacingCycleOutcome.HeldBack, "The record at the head of the queue has no dialer profile.");

            return null;
        }

        var profile = await services.GetRequiredService<IDialerProfileReader>().FindByIdAsync(head.DialerProfileId, cancellationToken);

        if (!IsPacedHere(profile))
        {
            // A profile agents dial, or one the predictive scheduler over-dials, is not this scheduler's to record.
            if (profile is null || !profile.Enabled)
            {
                await RecordAsync(
                    recorder,
                    queueId,
                    head.DialerProfileId,
                    startedUtc,
                    DialerPacingCycleOutcome.HeldBack,
                    profile is null
                        ? "The dialer profile of the record at the head of the queue no longer exists."
                        : "The dialer profile of the record at the head of the queue is turned off.");
            }

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

            // Each miss doubles the wait, up to a third of the lock's lifetime: retrying a held lock every second kept
            // reading the queue's head and profile, and recording a cycle, for as long as the holder took.
            var misses = _lockMisses.AddOrUpdate(queueId, 1, static (_, count) => count + 1);
            var backoff = TimeSpan.FromTicks(_options.LockRetryDelay.Ticks << Math.Min(misses - 1, 4));
            var cap = _options.LockExpiration / 3;

            Request(queueId, backoff < cap ? backoff : (cap > _options.LockRetryDelay ? cap : _options.LockRetryDelay));

            await RecordAsync(recorder, queueId, profile.ItemId, startedUtc, DialerPacingCycleOutcome.LockNotAcquired, "Another pacing cycle held the queue's pacing lock.");

            return null;
        }

        await using var acquiredLock = locker;

        _lockMisses.TryRemove(queueId, out _);

        var session = services.GetRequiredService<ISession>();
        var observation = recorder is null ? null : services.GetService<DialerPacingCycleObservation>();
        int started;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Pacing queue '{QueueId}' with dialer profile '{Profile}'.", queueId.SanitizeLogValue(), profile.Name);
        }

        using var cycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var cycleToken = cycleCancellation.Token;

        observation?.Begin();

        // The cycle is bounded because the lock it holds never expires in-process: a cycle waiting on something that never
        // answers would otherwise stop the campaign dialing until the application restarts.
        var cycle = RunCycleAsync();

        try
        {
            started = await cycle.WaitAsync(_options.CycleTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            await cycleCancellation.CancelAsync();

            // The abandoned cycle may still finish or fail later; its outcome is observed so it never goes unobserved.
            _ = cycle.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            _logger.LogWarning(
                "A pacing run of queue '{QueueId}' did not finish within {Timeout}; it was abandoned and none of its calls were placed. The minute dialer pacing task paces it again.",
                queueId.SanitizeLogValue(),
                _options.CycleTimeout);

            await session.CancelAsync();
            observation?.End();

            await RecordAsync(
                recorder,
                queueId,
                profile.ItemId,
                startedUtc,
                DialerPacingCycleOutcome.TimeBudgetExceeded,
                $"The cycle did not finish within {_options.CycleTimeout}; it was abandoned and none of its calls were placed.");

            return null;
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
            observation?.End();
            RequestRetry(queueId);

            await RecordAsync(
                recorder,
                queueId,
                profile.ItemId,
                startedUtc,
                DialerPacingCycleOutcome.WriteConflict,
                "Another transition saved an agent or record the cycle reserved first; none of its calls were placed.");

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "A pacing run of queue '{QueueId}' failed; none of its calls were placed. The minute dialer pacing task paces it again.", queueId.SanitizeLogValue());

            await session.CancelAsync();
            observation?.End();

            await RecordAsync(recorder, queueId, profile.ItemId, startedUtc, DialerPacingCycleOutcome.Failed, DescribeFailure(ex));

            return null;
        }

        observation?.End();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Paced queue '{QueueId}' on request: {Started} call(s) placed.", queueId.SanitizeLogValue(), started);
        }

        if (recorder is not null)
        {
            await recorder.RecordAsync(await DescribeCompletedCycleAsync(services, queueId, profile, startedUtc, started, observation, cancellationToken));
        }

        return started > 0 ? _clock.UtcNow.Add(_options.Debounce) : null;

        async Task<int> RunCycleAsync()
        {
            var placed = await services.GetRequiredService<IDialerService>().RunCycleAsync(profile, queueId, cycleToken);

            // Committed before the lock is released: the next cycle, here or on another node, reads the agents this one
            // reserved.
            await session.SaveChangesAsync(cycleToken);

            return placed;
        }
    }

    /// <summary>
    /// Describes a cycle that ran to the end and committed: how it ended, why, and the last call it placed and assigned.
    /// </summary>
    /// <param name="services">The services of the scope the cycle ran on.</param>
    /// <param name="queueId">The campaign queue.</param>
    /// <param name="profile">The dialer profile the cycle paced the queue with.</param>
    /// <param name="startedUtc">When the cycle started.</param>
    /// <param name="placed">How many calls the cycle placed.</param>
    /// <param name="observation">What the cycle was seen to decide, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The cycle's record.</returns>
    internal static async Task<DialerPacingCycle> DescribeCompletedCycleAsync(
        IServiceProvider services,
        string queueId,
        DialerProfile profile,
        DateTime startedUtc,
        int placed,
        DialerPacingCycleObservation observation,
        CancellationToken cancellationToken)
    {
        string configurationProblem = null;
        var policyForbidsDialing = false;

        if (placed == 0)
        {
            configurationProblem = DescribeConfigurationProblem(services, profile);

            // A Predictive cycle that never asked routing for an agent was stopped before it began by the abandonment
            // policy; asking the policy again says whether that is why.
            if (configurationProblem is null &&
                profile.Mode == DialerMode.Predictive &&
                (observation is null || observation.RoutingSucceeded + observation.RoutingNoAgent == 0))
            {
                policyForbidsDialing = await PolicyForbidsDialingAsync(services, profile, cancellationToken);
            }
        }

        var (outcome, detail) = DialerPacingCycleClassifier.Classify(placed, observation, configurationProblem, policyForbidsDialing);

        var record = new DialerPacingCycle
        {
            QueueId = queueId,
            DialerProfileId = profile.ItemId,
            Outcome = outcome,
            Detail = detail,
            CallsPlaced = placed,
            StartedUtc = startedUtc,
            RoutingSucceeded = observation?.RoutingSucceeded ?? 0,
            RoutingNoAgent = observation?.RoutingNoAgent ?? 0,
            Suppressed = observation?.Suppressed ?? 0,
            LastAssignedAgentId = observation?.LastAssignedAgentId,
            LastAssignedActivityItemId = observation?.LastAssignedActivityItemId,
        };

        await DescribeLastPlacedAsync(services, record, placed > 0 ? observation?.LastPlacedInteractionId : null, cancellationToken);

        return record;
    }

    /// <summary>
    /// Fills in the record and agent of the last call a cycle placed, from the interaction the call was placed as.
    /// </summary>
    /// <param name="services">The services of the scope the cycle ran on.</param>
    /// <param name="record">The cycle's record.</param>
    /// <param name="interactionId">The interaction of the last call the cycle placed, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    internal static async Task DescribeLastPlacedAsync(
        IServiceProvider services,
        DialerPacingCycle record,
        string interactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return;
        }

        record.LastPlacedInteractionId = interactionId;

        var interactionStore = services.GetService<IInteractionStore>();

        if (interactionStore is null)
        {
            return;
        }

        try
        {
            var interaction = await interactionStore.FindByIdAsync(interactionId, cancellationToken);

            record.LastPlacedActivityItemId = interaction?.ActivityItemId;
            record.LastPlacedAgentId = interaction?.AgentId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The call was placed whether or not the record can name who it was for.
        }
    }

    /// <summary>
    /// Describes a failure in a line short enough to store with the cycle's record.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The exception's type and message, cut to a bounded length.</returns>
    internal static string DescribeFailure(Exception exception)
    {
        var description = $"{exception.GetType().Name}: {exception.Message}";

        return description.Length <= MaxDetailLength
            ? description
            : description.Substring(0, MaxDetailLength);
    }

    private static string DescribeConfigurationProblem(IServiceProvider services, DialerProfile profile)
    {
        var router = services.GetService<IVoiceContactCenterCallRouter>();

        if (router is not null && !router.CanRouteOutbound(profile.ProviderName))
        {
            return "No Contact Center voice provider can route outbound calls for the dialer profile.";
        }

        var resolver = services.GetService<IDialerStrategyResolver>();

        if (resolver is not null && resolver.Resolve(profile.Mode) is null)
        {
            return $"The '{profile.Mode}' dialing mode is not enabled.";
        }

        return null;
    }

    private static async Task<bool> PolicyForbidsDialingAsync(IServiceProvider services, DialerProfile profile, CancellationToken cancellationToken)
    {
        var policy = services.GetService<IDialerAbandonmentPolicyService>();

        if (policy is null)
        {
            return false;
        }

        try
        {
            return !(await policy.EvaluateAsync(profile, cancellationToken)).IsPermitted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private static Task RecordAsync(
        IDialerPacingCycleRecorder recorder,
        string queueId,
        string dialerProfileId,
        DateTime startedUtc,
        DialerPacingCycleOutcome outcome,
        string detail)
    {
        if (recorder is null)
        {
            return Task.CompletedTask;
        }

        return recorder.RecordAsync(new DialerPacingCycle
        {
            QueueId = queueId,
            DialerProfileId = dialerProfileId,
            Outcome = outcome,
            Detail = detail,
            StartedUtc = startedUtc,
        });
    }
}
