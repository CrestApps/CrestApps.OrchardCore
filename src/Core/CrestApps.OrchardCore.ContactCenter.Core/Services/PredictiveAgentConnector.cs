using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IPredictiveAgentConnector"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two answered calls can want the same free agent, and an inbound offer can want them too. Every claim goes through
/// <see cref="IActivityReservationService.ClaimConnectedCallAsync"/>, which takes the agent's reservation lock, re-checks the
/// agent is still free and commits through the same compare-and-set as every other routing transition, so an agent is
/// only ever claimed once; the call that loses moves on to the next free agent, and with nobody left hears the
/// abandoned-call message.
/// </para>
/// <para>
/// A redelivered answer, the connect retried after its wait and the sweep that services waiting calls can all reach the
/// same call. A per-call lock taken without waiting lets one of them work at a time, and the claimed and abandoned marks on
/// the interaction make every later one a no-op.
/// </para>
/// </remarks>
public sealed class PredictiveAgentConnector : IPredictiveAgentConnector
{
    private const int MaxClaimAttempts = 3;

    private static readonly TimeSpan _connectRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IQueueItemStore _queueItemStore;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IActivityRoutingService _routingService;
    private readonly IActivityReservationService _reservationService;
    private readonly IDialerProfileReader _profileReader;
    private readonly IDialerAbandonmentTracker _abandonmentTracker;
    private readonly IProviderCommandStateService _providerCommandStateService;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IContactCenterDeadlineScheduler _deadlineScheduler;
    private readonly IPredictivePacingScheduler _pacingScheduler;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ContactCenterPredictiveDialingOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictiveAgentConnector"/> class.
    /// </summary>
    /// <param name="interactionManager">The interactions.</param>
    /// <param name="callSessionManager">The call sessions.</param>
    /// <param name="queueItemManager">The queue items.</param>
    /// <param name="queueItemStore">The queue item store, for the calls still waiting for an agent.</param>
    /// <param name="availabilityService">The authority on which agents may take work now.</param>
    /// <param name="routingService">The routing policy that orders the free agents.</param>
    /// <param name="reservationService">The reservation service that claims an agent.</param>
    /// <param name="profileReader">The dialer profiles, for the connect wait.</param>
    /// <param name="abandonmentTracker">The tracker that plays the abandoned-call message and counts the call abandoned.</param>
    /// <param name="providerCommandStateService">The durable provider commands, for the command that bridges the agent.</param>
    /// <param name="voiceProviderResolver">The voice providers, to know whether the agent joins through a leg of their own.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="scopeExecutor">The executor that runs work after commit and retries in a fresh scope.</param>
    /// <param name="deadlineScheduler">The in-process scheduler that retries the connect while the profile waits.</param>
    /// <param name="pacingScheduler">The scheduler that paces the queue again once a call is settled.</param>
    /// <param name="distributedLock">The lock that lets one delivery connect a call at a time.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The predictive dialing options.</param>
    /// <param name="logger">The logger.</param>
    public PredictiveAgentConnector(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IQueueItemManager queueItemManager,
        IQueueItemStore queueItemStore,
        IAgentAvailabilityService availabilityService,
        IActivityRoutingService routingService,
        IActivityReservationService reservationService,
        IDialerProfileReader profileReader,
        IDialerAbandonmentTracker abandonmentTracker,
        IProviderCommandStateService providerCommandStateService,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher,
        IContactCenterScopeExecutor scopeExecutor,
        IContactCenterDeadlineScheduler deadlineScheduler,
        IPredictivePacingScheduler pacingScheduler,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        IOptions<ContactCenterPredictiveDialingOptions> options,
        ILogger<PredictiveAgentConnector> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _queueItemManager = queueItemManager;
        _queueItemStore = queueItemStore;
        _availabilityService = availabilityService;
        _routingService = routingService;
        _reservationService = reservationService;
        _profileReader = profileReader;
        _abandonmentTracker = abandonmentTracker;
        _providerCommandStateService = providerCommandStateService;
        _voiceProviderResolver = voiceProviderResolver;
        _publisher = publisher;
        _scopeExecutor = scopeExecutor;
        _deadlineScheduler = deadlineScheduler;
        _pacingScheduler = pacingScheduler;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// The key of the lock one delivery holds while it connects a call.
    /// </summary>
    /// <param name="interactionId">The interaction of the call.</param>
    public static string GetConnectLockKey(string interactionId)
        => $"ContactCenterPredictiveConnect:{interactionId}";

    /// <summary>
    /// The key of the in-process deadline that retries the connect while the profile's connect wait runs.
    /// </summary>
    /// <param name="interactionId">The interaction of the call.</param>
    public static string GetConnectDeadlineKey(string interactionId)
        => $"predictive-connect:{interactionId}";

    /// <inheritdoc/>
    public Task<PredictiveConnectOutcome> ConnectAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        return ConnectAttemptAsync(interactionId, attempt: 1, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ServiceWaitingAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var serviced = 0;

        foreach (var item in await _queueItemStore.GetDialerInFlightAsync(queueId, cancellationToken))
        {
            var interaction = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);

            // Only a call a person answered and nobody has dealt with is waiting; one still ringing is not.
            if (interaction is null ||
                interaction.IsSettled ||
                !DialerCallMetadata.IsOverDialed(interaction) ||
                DialerCallMetadata.GetLiveAnsweredUtc(interaction) is null ||
                DialerCallMetadata.IsAgentClaimed(interaction) ||
                DialerCallMetadata.IsAbandoned(interaction))
            {
                continue;
            }

            var outcome = await ConnectAsync(interaction.ItemId, cancellationToken);

            if (outcome is PredictiveConnectOutcome.Claimed or PredictiveConnectOutcome.Abandoned)
            {
                serviced++;
            }
        }

        return serviced;
    }

    internal async Task<PredictiveConnectOutcome> ConnectAttemptAsync(string interactionId, int attempt, CancellationToken cancellationToken)
    {
        try
        {
            return await ConnectUnderLockAsync(interactionId, cancellationToken);
        }
        catch (ConcurrencyException) when (attempt < MaxClaimAttempts)
        {
            // Another transition changed the agent, the call or the queue item between the read and the commit; this
            // scope's session is spent. The next attempt starts over in a fresh scope and reads what won.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Connecting over-dialed call '{InteractionId}' lost a race to another transition (attempt {Attempt}); trying again.",
                    interactionId.SanitizeLogValue(),
                    attempt);
            }

            var outcome = PredictiveConnectOutcome.NotApplicable;

            await _scopeExecutor.ExecuteAsync<IPredictiveAgentConnector>(async connector =>
            {
                outcome = connector is PredictiveAgentConnector predictive
                    ? await predictive.ConnectAttemptAsync(interactionId, attempt + 1, cancellationToken)
                    : await connector.ConnectAsync(interactionId, cancellationToken);
            });

            return outcome;
        }
    }

    private async Task<PredictiveConnectOutcome> ConnectUnderLockAsync(string interactionId, CancellationToken cancellationToken)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetConnectLockKey(interactionId),
            TimeSpan.Zero,
            _options.PacingLockExpiration);

        if (!locked)
        {
            return PredictiveConnectOutcome.InProgress;
        }

        await using var acquiredLock = locker;

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null ||
            interaction.IsSettled ||
            !DialerCallMetadata.IsOverDialed(interaction) ||
            DialerCallMetadata.WasAnsweredByMachine(interaction))
        {
            return PredictiveConnectOutcome.NotApplicable;
        }

        if (DialerCallMetadata.IsAgentClaimed(interaction) ||
            DialerCallMetadata.IsAbandoned(interaction) ||
            !string.IsNullOrEmpty(interaction.AgentId))
        {
            return PredictiveConnectOutcome.AlreadyHandled;
        }

        var liveAnsweredUtc = DialerCallMetadata.GetLiveAnsweredUtc(interaction);

        if (liveAnsweredUtc is null)
        {
            return PredictiveConnectOutcome.NotApplicable;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (session is null || CallSessionLifecycle.IsTerminal(session.State) || string.IsNullOrEmpty(session.ProviderCallId))
        {
            return PredictiveConnectOutcome.NotApplicable;
        }

        var queueItem = await _queueItemManager.FindByActivityIdAsync(interaction.ActivityItemId, cancellationToken);

        if (queueItem is not null &&
            queueItem.Status == QueueItemStatus.Assigned &&
            string.IsNullOrEmpty(queueItem.AgentId) &&
            await TryClaimAsync(queueItem, interaction, session, liveAnsweredUtc.Value, cancellationToken))
        {
            return PredictiveConnectOutcome.Claimed;
        }

        var profile = await _profileReader.FindByIdAsync(DialerCallMetadata.GetDialerProfileId(interaction), cancellationToken);
        var waitUntilUtc = liveAnsweredUtc.Value.AddMilliseconds(Math.Max(0, profile?.ConnectWaitMilliseconds ?? 0));

        if (_clock.UtcNow < waitUntilUtc)
        {
            // The profile chose to hold the person briefly for an agent about to free up. An agent who becomes free in
            // the meantime services this call first; otherwise it is tried again until the wait runs out.
            _deadlineScheduler.Schedule(
                GetConnectDeadlineKey(interaction.ItemId),
                _clock.UtcNow.Add(_connectRetryDelay),
                async (services, token) =>
                {
                    await services.GetRequiredService<IPredictiveAgentConnector>().ConnectAsync(interactionId, token);

                    return null;
                });

            return PredictiveConnectOutcome.Waiting;
        }

        await AbandonAsync(interaction, session, queueItem?.QueueId ?? interaction.QueueId, cancellationToken);

        return PredictiveConnectOutcome.Abandoned;
    }

    private async Task<bool> TryClaimAsync(
        QueueItem queueItem,
        Interaction interaction,
        CallSession session,
        DateTime liveAnsweredUtc,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in await GetCandidatesAsync(queueItem, cancellationToken))
        {
            string commandId = null;

            var reservation = await _reservationService.ClaimConnectedCallAsync(
                queueItem,
                candidate,
                interaction.ItemId,
                _options.ConnectLockWait,
                async (claimed, agent) =>
                {
                    session.AgentId = agent.ItemId;
                    session.QueueId ??= queueItem.QueueId;

                    if (ProviderJoinsAgentAfterAnswer(session))
                    {
                        commandId = await AnsweredCallBridge.RegisterAsync(
                            _providerCommandStateService,
                            _callSessionManager,
                            session,
                            interaction,
                            agent.ItemId,
                            agent.UserId,
                            claimed.ItemId,
                            cancellationToken);
                    }
                    else
                    {
                        // A provider that puts the agent on the call itself has nothing more to bridge.
                        DialerCallMetadata.MarkAgentJoined(interaction, _clock.UtcNow);
                        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
                    }

                    await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
                    await PublishClaimedAsync(interaction, agent, claimed, liveAnsweredUtc, cancellationToken);
                },
                cancellationToken);

            if (reservation is null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(commandId))
            {
                _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
                    processor.DispatchAsync(commandId, CancellationToken.None));
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Connecting agent '{AgentId}' to over-dialed call '{ProviderCallId}' (interaction '{InteractionId}') {Milliseconds:0} ms after the person answered.",
                    reservation.AgentId.SanitizeLogValue(),
                    session.ProviderCallId.SanitizeLogValue(),
                    interaction.ItemId.SanitizeLogValue(),
                    (_clock.UtcNow - liveAnsweredUtc).TotalMilliseconds);
            }

            return true;
        }

        return false;
    }

    // The free agents of the campaign, in the order routing would offer them the call: its pick first, then the other
    // eligible agents by score, the one idle longest first among equals.
    private async Task<IReadOnlyList<AgentProfile>> GetCandidatesAsync(QueueItem queueItem, CancellationToken cancellationToken)
    {
        var availability = (await _availabilityService.GetForQueueAsync(queueItem.QueueId, cancellationToken))
            .Where(candidate => candidate?.Agent is not null && string.IsNullOrWhiteSpace(candidate.Agent.ActiveReservationId))
            .ToArray();

        if (availability.Length == 0)
        {
            return [];
        }

        var ordered = new List<AgentProfile>(availability.Length);

        try
        {
            var decision = await _routingService.SelectAgentAsync(
                CampaignRoutingQueue.Create(queueItem.QueueId),
                queueItem,
                availability,
                cancellationToken);

            if (decision?.Succeeded == true && decision.Agent is not null)
            {
                ordered.Add(decision.Agent);
            }

            if (decision?.Candidates is not null)
            {
                ordered.AddRange(decision.Candidates
                    .Where(candidate => candidate.IsEligible && candidate.Agent is not null)
                    .OrderByDescending(candidate => candidate.Score)
                    .ThenBy(candidate => candidate.Agent.LastAssignedUtc ?? DateTime.MinValue)
                    .Select(candidate => candidate.Agent)
                    .Where(agent => !ordered.Any(existing => string.Equals(existing.ItemId, agent.ItemId, StringComparison.Ordinal))));
            }

            return ordered;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The person is on the line: with routing unable to say, the agent idle longest takes the call.
            _logger.LogWarning(ex, "Routing could not order the free agents of campaign queue '{QueueId}'; the agent idle longest is tried first.", queueItem.QueueId.SanitizeLogValue());

            return availability
                .OrderBy(candidate => candidate.Agent.LastAssignedUtc ?? DateTime.MinValue)
                .Select(candidate => candidate.Agent)
                .ToArray();
        }
    }

    private async Task AbandonAsync(Interaction interaction, CallSession session, string queueId, CancellationToken cancellationToken)
    {
        _deadlineScheduler.Cancel(GetConnectDeadlineKey(interaction.ItemId));

        // The message starts before anything is written: the person is listening to silence until it does.
        var messageStarted = await _abandonmentTracker.AbandonAsync(
            interaction,
            session.ProviderName,
            session.ProviderCallId,
            DialerAbandonment.Reasons.NoAgentAvailable,
            cancellationToken);

        // A call nobody could take is abandoned whatever else happens; the mark is what keeps a later connect, a retry
        // or the sweep from acting on it again.
        DialerCallMetadata.MarkAbandoned(interaction, DialerAbandonment.Reasons.NoAgentAvailable);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        if (!messageStarted)
        {
            await HangUpAsync(session, interaction);
        }

        _logger.LogWarning(
            "No agent was free for over-dialed call '{ProviderCallId}' (interaction '{InteractionId}'); it was abandoned {How}.",
            session.ProviderCallId.SanitizeLogValue(),
            interaction.ItemId.SanitizeLogValue(),
            messageStarted ? "with the abandoned-call message" : "and hung up, because no abandoned-call message could be played");

        if (!string.IsNullOrEmpty(queueId))
        {
            _pacingScheduler.Request(queueId);
        }
    }

    private async Task HangUpAsync(CallSession session, Interaction interaction)
    {
        var providerName = session.ProviderName;
        var providerCallId = session.ProviderCallId;

        await _scopeExecutor.ExecuteAsync<ITelephonyProviderResolver>(async resolver =>
        {
            try
            {
                if (await resolver.GetAsync(providerName) is not ITelephonyCallControlProvider callControl)
                {
                    return;
                }

                var result = await callControl.HangupAsync(new CallReference { CallId = providerCallId }, CancellationToken.None);

                if (!result.Succeeded)
                {
                    _logger.LogWarning(
                        "The provider did not confirm hanging up abandoned call '{ProviderCallId}' (interaction '{InteractionId}'): {Error}.",
                        providerCallId.SanitizeLogValue(),
                        interaction.ItemId.SanitizeLogValue(),
                        result.Error.SanitizeLogValue());
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not hang up abandoned call '{ProviderCallId}'.", providerCallId.SanitizeLogValue());
            }
        });
    }

    private bool ProviderJoinsAgentAfterAnswer(CallSession session)
    {
        var provider = _voiceProviderResolver.Get(session.ProviderName);

        return provider is not null &&
            provider.DeliveryModel == VoiceProviderDeliveryModel.ServerSideAcd &&
            provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.AgentConnect) &&
            provider is IContactCenterVoiceCallControlProvider;
    }

    private Task PublishClaimedAsync(
        Interaction interaction,
        AgentProfile agent,
        ActivityReservation reservation,
        DateTime liveAnsweredUtc,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var data = ContactCenterCallAudit.ForInteraction(interaction);
        data.AgentId = agent.ItemId;
        data.DurationSeconds = Math.Round(Math.Max(0, (now - liveAnsweredUtc).TotalSeconds), 3);
        data.Details["dialerProfileId"] = DialerCallMetadata.GetDialerProfileId(interaction);
        data.Details["reservationId"] = reservation.ItemId;

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.DialerAgentConnectClaimed,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(DialerProfile),
            AggregateId = DialerCallMetadata.GetDialerProfileId(interaction),
            OccurredUtc = now,
            ActorId = ContactCenterConstants.SystemActor,
            ActorType = ContactCenterActorType.System,
            SourceComponent = ContactCenterConstants.Components.Dialer,
            IdempotencyKey = $"dialer:{ContactCenterConstants.Events.DialerAgentConnectClaimed}:{interaction.ItemId}",
        };

        interactionEvent.SetData(data);

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }
}
