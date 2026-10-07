using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IPredictiveOverDialPacer"/>.
/// </summary>
/// <remarks>
/// <para>
/// One cycle: take the queue's pacing lock without waiting; measure the free agents, the agents about to free up, the
/// calls already in flight, the answer rate and the abandonment rates; let <see cref="PredictiveOverDialCalculator"/>
/// decide; place that many calls without an agent from the head of the queue; then rewrite the queue's
/// <see cref="PredictivePacingState"/> and commit, all before the lock is released.
/// </para>
/// <para>
/// Without the Redis lock feature the lock is only process-local, so it cannot by itself stop two nodes pacing the same
/// queue. The pacing record can: it is rewritten every cycle under a concurrency check, so of two cycles that raced only
/// one commits, and because a call is only dispatched after its transaction commits, the losing cycle's calls are never
/// placed. Every doubt fails closed: a lock held elsewhere, a missing statistic, a conflict or an error places no call
/// without an agent.
/// </para>
/// </remarks>
public sealed class PredictiveOverDialPacer : IPredictiveOverDialPacer
{
    private const int FallbackReservationTimeoutSeconds = 30;

    private readonly IDialerAbandonmentPolicyService _abandonmentPolicy;
    private readonly IEnumerable<IDialerAbandonmentStatisticsProvider> _abandonmentStatisticsProviders;
    private readonly IDialerPacingStatisticsProvider _pacingStatisticsProvider;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IAgentProfileManager _agentManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IQueueItemStore _queueItemStore;
    private readonly IQueuedWorkWithdrawalService _withdrawalService;
    private readonly IQueuedDialerWorkGate _dialerWorkGate;
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityReservationService _reservationService;
    private readonly IDialerAttemptService _attemptService;
    private readonly IPredictivePacingStateStore _stateStore;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IPredictivePacingScheduler _pacingScheduler;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ContactCenterPredictiveDialingOptions _options;
    private readonly ContactCenterComplianceOptions _complianceOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictiveOverDialPacer"/> class.
    /// </summary>
    /// <param name="abandonmentPolicy">The abandonment policy, which may forbid dialing at all.</param>
    /// <param name="abandonmentStatisticsProviders">The abandonment statistics the cap is enforced on.</param>
    /// <param name="pacingStatisticsProvider">The answer rate and timings the calls are sized from.</param>
    /// <param name="availabilityService">The authority on which agents may take work now.</param>
    /// <param name="agentManager">The agents, for the busy agents about to free up.</param>
    /// <param name="queueItemManager">The queue items.</param>
    /// <param name="queueItemStore">The queue item store, for the calls in flight.</param>
    /// <param name="withdrawalService">The service that withdraws a waiting item whose activity is gone.</param>
    /// <param name="dialerWorkGate">The gate that holds back a record that may not be dialed yet.</param>
    /// <param name="interactionManager">The interactions, to find a retry of an abandoned call.</param>
    /// <param name="reservationService">The reservation service, for a retry that must have an agent.</param>
    /// <param name="attemptService">The attempt service that places each call.</param>
    /// <param name="stateStore">The pacing records.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="pacingScheduler">The scheduler that paces the queue again shortly when its lock was held.</param>
    /// <param name="distributedLock">The pacing lock.</param>
    /// <param name="session">The YesSql session the cycle commits.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The predictive dialing options.</param>
    /// <param name="complianceOptions">The compliance options, for the rolling abandonment window.</param>
    /// <param name="logger">The logger.</param>
    public PredictiveOverDialPacer(
        IDialerAbandonmentPolicyService abandonmentPolicy,
        IEnumerable<IDialerAbandonmentStatisticsProvider> abandonmentStatisticsProviders,
        IDialerPacingStatisticsProvider pacingStatisticsProvider,
        IAgentAvailabilityService availabilityService,
        IAgentProfileManager agentManager,
        IQueueItemManager queueItemManager,
        IQueueItemStore queueItemStore,
        IQueuedWorkWithdrawalService withdrawalService,
        IQueuedDialerWorkGate dialerWorkGate,
        IInteractionManager interactionManager,
        IActivityReservationService reservationService,
        IDialerAttemptService attemptService,
        IPredictivePacingStateStore stateStore,
        IContactCenterEventPublisher publisher,
        IPredictivePacingScheduler pacingScheduler,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        IOptions<ContactCenterPredictiveDialingOptions> options,
        IOptions<ContactCenterComplianceOptions> complianceOptions,
        ILogger<PredictiveOverDialPacer> logger)
    {
        _abandonmentPolicy = abandonmentPolicy;
        _abandonmentStatisticsProviders = abandonmentStatisticsProviders;
        _pacingStatisticsProvider = pacingStatisticsProvider;
        _availabilityService = availabilityService;
        _agentManager = agentManager;
        _queueItemManager = queueItemManager;
        _queueItemStore = queueItemStore;
        _withdrawalService = withdrawalService;
        _dialerWorkGate = dialerWorkGate;
        _interactionManager = interactionManager;
        _reservationService = reservationService;
        _attemptService = attemptService;
        _stateStore = stateStore;
        _publisher = publisher;
        _pacingScheduler = pacingScheduler;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _options = options.Value;
        _complianceOptions = complianceOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// The key of the lock a cycle holds while it paces the queue.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    public static string GetPacingLockKey(string queueId)
        => $"ContactCenterPredictivePacing:{queueId}";

    /// <inheritdoc/>
    public async Task<PredictivePacingCycleResult> RunCycleAsync(DialerProfile profile, string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetPacingLockKey(queueId),
            TimeSpan.Zero,
            _options.PacingLockExpiration);

        if (!locked)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipped an over-dial cycle of queue '{QueueId}' because another cycle holds its pacing lock; it is paced again shortly.", queueId.SanitizeLogValue());
            }

            // The holder may be finishing a cycle that measured the queue before the change that asked for this one, so the
            // queue is paced again shortly rather than left for the next event.
            _pacingScheduler.RequestRetry(queueId);

            return PredictivePacingCycleResult.NotRun;
        }

        await using var acquiredLock = locker;

        try
        {
            var result = await RunLockedCycleAsync(profile, queueId, cancellationToken);

            // Committed before the lock is released: the next cycle, here or on another node, reads these calls in flight.
            await _session.SaveChangesAsync(cancellationToken);

            return result;
        }
        catch (ConcurrencyException)
        {
            // Another node committed a cycle of this queue first. This cycle's calls were rolled back with it and are
            // never dispatched; the next cycle measures what the winner placed.
            _logger.LogWarning(
                "An over-dial cycle of queue '{QueueId}' lost a race with another cycle; none of its calls were placed.",
                queueId.SanitizeLogValue());

            return PredictivePacingCycleResult.NotRun;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "An over-dial cycle of queue '{QueueId}' failed; none of its calls were placed.", queueId.SanitizeLogValue());

            // Nothing the cycle staged may commit with the scope: its calls would be placed without the pacing record
            // that guards them.
            await _session.CancelAsync();

            return PredictivePacingCycleResult.NotRun;
        }
    }

    private async Task<PredictivePacingCycleResult> RunLockedCycleAsync(DialerProfile profile, string queueId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var input = PredictivePacingInput.ForProfile(profile, _options);

        var policy = await _abandonmentPolicy.EvaluateAsync(profile, cancellationToken);
        input.PolicyPermitted = policy.IsPermitted;

        var pacing = await _pacingStatisticsProvider.GetStatisticsAsync(
            profile.ItemId,
            TimeSpan.FromMinutes(Math.Max(1, profile.AnswerRateWindowMinutes)),
            cancellationToken);

        input.AnswerRate = pacing?.AnswerRate;
        input.AnswerRateSampleSize = pacing?.SettledAttempts ?? 0;

        var rolling = await GetAbandonmentStatisticsAsync(profile.ItemId, TimeSpan.FromMinutes(Math.Max(1, _complianceOptions.AbandonmentRollingWindowMinutes)), cancellationToken);
        input.AbandonmentRatePercent = RatePercent(rolling);
        input.AbandonmentSampleSize = rolling?.LiveAnswers ?? 0;

        var compliance = await GetAbandonmentStatisticsAsync(profile.ItemId, TimeSpan.FromDays(Math.Max(1, _options.ComplianceWindowDays)), cancellationToken);
        input.ComplianceAbandonmentRatePercent = RatePercent(compliance);

        var available = (await _availabilityService.GetForQueueAsync(queueId, cancellationToken))
            .Where(candidate => candidate?.Agent is not null && string.IsNullOrWhiteSpace(candidate.Agent.ActiveReservationId))
            .Select(candidate => candidate.Agent)
            .OrderBy(agent => agent.LastAssignedUtc ?? DateTime.MinValue)
            .ToList();

        if (_options.DiscountAgentsWithWaitingInbound)
        {
            available = await WithoutAgentsOwedToInboundAsync(available, cancellationToken);
        }

        input.AvailableAgents = available.Count;
        input.FreeingAgents = profile.CreditAgentsFreeingUp
            ? await CountFreeingAsync(queueId, pacing, now, cancellationToken)
            : 0;
        input.CallsInFlight = await _queueItemStore.CountDialerInFlightAsync(queueId, cancellationToken);

        var decision = PredictiveOverDialCalculator.Calculate(input);
        var dialed = 0;

        if (decision.Mode == PredictivePacingDecisionMode.OverDial && decision.DialCount > 0)
        {
            dialed = await DialAsync(profile, queueId, decision.DialCount, available, now, cancellationToken);
        }

        await RecordAsync(profile, queueId, input, decision, dialed, now, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Over-dial cycle of queue '{QueueId}' (profile '{ProfileId}'): {Mode} ({Reason}); agents {Available} free + {Freeing} freeing, {InFlight} in flight, answer rate {AnswerRate:0.###}, abandonment {Rolling:0.##}% rolling / {Compliance:0.##}% compliance; target {Target} calls, {DialCount} allowed, {Dialed} placed.",
                queueId.SanitizeLogValue(),
                profile.ItemId.SanitizeLogValue(),
                decision.Mode,
                decision.Reason,
                input.AvailableAgents,
                input.FreeingAgents,
                input.CallsInFlight,
                input.AnswerRate,
                input.AbandonmentRatePercent,
                input.ComplianceAbandonmentRatePercent,
                decision.TargetCalls,
                decision.DialCount,
                dialed);
        }

        return new PredictivePacingCycleResult
        {
            Decision = decision,
            Dialed = dialed,
        };
    }

    private async Task<int> DialAsync(
        DialerProfile profile,
        string queueId,
        int dialCount,
        List<AgentProfile> available,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var queue = CampaignRoutingQueue.Create(queueId);
        var heldBack = new HashSet<string>(StringComparer.Ordinal);
        var dialed = 0;
        var stagedUnreserved = false;

        while (dialed < dialCount)
        {
            var item = await RoutableQueueHead.NextAsync(_queueItemManager, _withdrawalService, _dialerWorkGate, queue, now, heldBack, cancellationToken);

            if (item is null)
            {
                break;
            }

            // An abandoned call is not tried again without an agent waiting for the person: its retry is placed the
            // reserve-then-dial way, for the free agent idle longest, or waits for the next cycle when nobody is free.
            //
            // A reservation commits on its own. Once this cycle has staged a call without an agent, that commit would
            // carry the staged call with it ahead of the pacing record that guards it, so the retry waits for the next
            // cycle instead.
            if (profile.AbandonedRetryRequiresAgent && (item.RequiresReservedAgent || await WasAbandonedAsync(item, cancellationToken)))
            {
                if (stagedUnreserved || !await DialWithReservedAgentAsync(profile, item, available, cancellationToken))
                {
                    break;
                }

                dialed++;

                continue;
            }

            if (await _attemptService.TryDialUnreservedAsync(profile, item, cancellationToken))
            {
                stagedUnreserved = true;
                dialed++;

                continue;
            }

            // An item the gate or a race kept from being dialed stays at the head of the queue; reading it again would
            // only refuse it again, so the cycle ends here and the next one tries.
            var current = await _queueItemManager.FindByIdAsync(item.ItemId, cancellationToken);

            if (current?.Status == QueueItemStatus.Waiting)
            {
                break;
            }
        }

        return dialed;
    }

    // An agent signed in to an inbound queue with calls waiting is likely to be given one of them before an answered
    // campaign call reaches them, so sizing the over-dial on them would abandon the campaign call instead.
    private async Task<List<AgentProfile>> WithoutAgentsOwedToInboundAsync(List<AgentProfile> available, CancellationToken cancellationToken)
    {
        var inboundQueueIds = available
            .SelectMany(agent => agent.QueueIds ?? [])
            .Where(queueId => !string.IsNullOrEmpty(queueId) && !ContactCenterConstants.IsCampaignQueue(queueId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (inboundQueueIds.Length == 0)
        {
            return available;
        }

        var waiting = await _queueItemStore.CountWaitingByQueueIdsAsync(inboundQueueIds, cancellationToken);
        var busyQueues = new HashSet<string>(
            waiting.Where(pair => pair.Value > 0).Select(pair => pair.Key),
            StringComparer.OrdinalIgnoreCase);

        if (busyQueues.Count == 0)
        {
            return available;
        }

        return available
            .Where(agent => !(agent.QueueIds ?? []).Any(busyQueues.Contains))
            .ToList();
    }

    private async Task<bool> WasAbandonedAsync(QueueItem item, CancellationToken cancellationToken)
    {
        var previous = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);

        return previous is not null && DialerCallMetadata.IsAbandoned(previous);
    }

    private async Task<bool> DialWithReservedAgentAsync(DialerProfile profile, QueueItem item, List<AgentProfile> available, CancellationToken cancellationToken)
    {
        while (available.Count > 0)
        {
            var agent = available[0];
            available.RemoveAt(0);

            var reservation = await _reservationService.ReserveAsync(item, agent, FallbackReservationTimeoutSeconds, cancellationToken);

            if (reservation is null)
            {
                continue;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Retrying abandoned activity '{ActivityItemId}' with agent '{AgentId}' reserved for it, as profile '{ProfileId}' requires.",
                    item.ActivityItemId.SanitizeLogValue(),
                    agent.ItemId.SanitizeLogValue(),
                    profile.ItemId.SanitizeLogValue());
            }

            return await _attemptService.TryDialAsync(profile, reservation, cancellationToken);
        }

        return false;
    }

    private async Task<int> CountFreeingAsync(string queueId, DialerPacingStatistics pacing, DateTime now, CancellationToken cancellationToken)
    {
        var members = await _agentManager.GetMembersForQueueAsync(queueId, cancellationToken);
        var snapshots = members
            .Where(agent => agent.QueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true &&
                agent.PresenceStatus is AgentPresenceStatus.Busy or AgentPresenceStatus.WrapUp &&
                agent.PresenceChangedUtc.HasValue)
            .Select(agent => new AgentWorkSnapshot
            {
                AgentId = agent.ItemId,
                Phase = agent.PresenceStatus == AgentPresenceStatus.WrapUp ? AgentWorkPhase.WrappingUp : AgentWorkPhase.Talking,
                Elapsed = now - agent.PresenceChangedUtc.Value,
            });

        return AgentFreeUpPredictor.CountFreeingWithin(
            snapshots,
            pacing?.AverageTalkTime,
            pacing?.AverageWrapUpTime,
            pacing?.MedianRingToAnswer ?? _options.DefaultRingHorizon);
    }

    private async Task RecordAsync(
        DialerProfile profile,
        string queueId,
        PredictivePacingInput input,
        PredictivePacingDecision decision,
        int dialed,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var state = await _stateStore.FindByQueueIdAsync(queueId, cancellationToken);
        var isNew = state is null;

        state ??= new PredictivePacingState
        {
            ItemId = IdGenerator.GenerateId(),
            QueueId = queueId,
        };

        var previousMode = state.LastDecision?.Mode;

        state.DialerProfileId = profile.ItemId;
        state.Sequence++;
        state.LastCycleUtc = now;
        state.LastDecision = new PredictivePacingSnapshot
        {
            Mode = decision.Mode,
            Reason = decision.Reason,
            AvailableAgents = input.AvailableAgents,
            FreeingAgents = input.FreeingAgents,
            CallsInFlight = input.CallsInFlight,
            AnswerRate = input.AnswerRate,
            AbandonmentRatePercent = input.AbandonmentRatePercent,
            ComplianceAbandonmentRatePercent = input.ComplianceAbandonmentRatePercent,
            TargetCalls = decision.TargetCalls,
            DialCount = decision.DialCount,
            Dialed = dialed,
            Throttle = decision.Throttle,
            ExpectedAbandonmentPercent = decision.ExpectedAbandonmentPercent,
        };

        // Written every cycle, so every cycle is checked against a concurrent one when it commits.
        if (isNew)
        {
            await _stateStore.CreateAsync(state, cancellationToken);
        }
        else
        {
            await _stateStore.UpdateAsync(state, cancellationToken);
        }

        if (previousMode != decision.Mode)
        {
            var modeChanged = new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.DialerPacingModeChanged,
                AggregateType = nameof(DialerProfile),
                AggregateId = profile.ItemId,
                OccurredUtc = now,
                ActorId = ContactCenterConstants.SystemActor,
                ActorType = ContactCenterActorType.System,
                SourceComponent = ContactCenterConstants.Components.Dialer,
                IdempotencyKey = $"dialer:{ContactCenterConstants.Events.DialerPacingModeChanged}:{state.ItemId}:{state.Sequence}",
            };

            modeChanged.SetData(new PredictivePacingModeChangedEventData
            {
                QueueId = queueId,
                PreviousMode = previousMode,
                Mode = decision.Mode,
                Reason = decision.Reason,
            });

            await _publisher.PublishAsync(modeChanged, cancellationToken);
        }
    }

    private async Task<DialerAbandonmentStatistics> GetAbandonmentStatisticsAsync(string profileId, TimeSpan window, CancellationToken cancellationToken)
    {
        foreach (var provider in _abandonmentStatisticsProviders)
        {
            var statistics = await provider.GetStatisticsAsync(profileId, window, cancellationToken);

            if (statistics is not null)
            {
                return statistics;
            }
        }

        return null;
    }

    private static double? RatePercent(DialerAbandonmentStatistics statistics)
        => statistics is null || statistics.LiveAnswers <= 0
            ? null
            : (double)statistics.AbandonedCalls / statistics.LiveAnswers * 100;
}
