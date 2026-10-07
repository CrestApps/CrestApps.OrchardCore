using System.Globalization;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerAttemptService"/>. Every attempt runs the
/// outbound compliance gate first; eligible attempts are routed through the Voice Contact Center Call
/// Router, while suppressed attempts release the reservation and record an auditable suppression event.
/// </summary>
public sealed class DialerAttemptService : IDialerAttemptService
{
    private readonly IDialerEligibilityService _eligibilityService;
    private readonly IActivityReservationService _reservationService;
    private readonly IDialerAttemptCompensationService _compensationService;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IContactCenterActivityWriter _activityWriter;
    private readonly IAgentProfileManager _agentManager;
    private readonly IVoiceContactCenterCallRouter _voiceCallRouter;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IProviderCommandStateService _providerCommandStateService;
    private readonly IOutboundLineResolver _outboundLineResolver;
    private readonly IOmnichannelChannelEndpointManager _addressManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ContactCenterCoordinationOptions _coordinationOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerAttemptService"/> class.
    /// </summary>
    /// <param name="eligibilityService">The compliance gate evaluated before every attempt.</param>
    /// <param name="reservationService">The reservation service used to release failed or suppressed attempts.</param>
    /// <param name="compensationService">The service used to release failed or suppressed attempts.</param>
    /// <param name="interactionManager">The interaction manager used to record attempts.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="activityWriter">The writer used to apply CRM activity changes outside the routing transaction.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the reserved agent.</param>
    /// <param name="voiceCallRouter">The voice call router.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="auditRecorder">The recorder that writes each interaction a dial creates to the audit log.</param>
    /// <param name="scopeExecutor">The executor used for compensation and post-commit command wake-up.</param>
    /// <param name="providerCommandStateService">The service used to persist provider command intent.</param>
    /// <param name="addressManagers">The address list, which holds the number a load dials from, when the feature is on.</param>
    /// <param name="queueItemManager">The queue items, claimed by a call placed before any agent is reserved.</param>
    /// <param name="distributedLock">The lock that serializes claiming an activity with routing reserving it.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="coordinationOptions">The coordination options carrying the reservation lock timings.</param>
    /// <param name="logger">The logger instance.</param>
    public DialerAttemptService(
        IDialerEligibilityService eligibilityService,
        IActivityReservationService reservationService,
        IDialerAttemptCompensationService compensationService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IContactCenterWorkStateService workStateService,
        IContactCenterActivityWriter activityWriter,
        IAgentProfileManager agentManager,
        IVoiceContactCenterCallRouter voiceCallRouter,
        IContactCenterEventPublisher publisher,
        IContactCenterAuditRecorder auditRecorder,
        IContactCenterScopeExecutor scopeExecutor,
        IProviderCommandStateService providerCommandStateService,
        IEnumerable<IOmnichannelChannelEndpointManager> addressManagers,
        IQueueItemManager queueItemManager,
        IDistributedLock distributedLock,
        IClock clock,
        IOptions<ContactCenterCoordinationOptions> coordinationOptions,
        ILogger<DialerAttemptService> logger)
    {
        _eligibilityService = eligibilityService;
        _reservationService = reservationService;
        _compensationService = compensationService;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _workStateService = workStateService;
        _activityWriter = activityWriter;
        _agentManager = agentManager;
        _voiceCallRouter = voiceCallRouter;
        _publisher = publisher;
        _auditRecorder = auditRecorder;
        _scopeExecutor = scopeExecutor;
        _providerCommandStateService = providerCommandStateService;
        // The address list is a feature of its own; without it a load cannot have picked a number.
        _addressManager = addressManagers.FirstOrDefault();
        _queueItemManager = queueItemManager;
        _distributedLock = distributedLock;
        _clock = clock;
        _coordinationOptions = coordinationOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerAttemptService"/> class that presents each agent's own
    /// outbound line on the calls dialed for them.
    /// </summary>
    /// <param name="eligibilityService">The compliance gate evaluated before every attempt.</param>
    /// <param name="reservationService">The reservation service used to release failed or suppressed attempts.</param>
    /// <param name="compensationService">The service used to release failed or suppressed attempts.</param>
    /// <param name="interactionManager">The interaction manager used to record attempts.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="activityWriter">The writer used to apply CRM activity changes outside the routing transaction.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the reserved agent.</param>
    /// <param name="voiceCallRouter">The voice call router.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="auditRecorder">The recorder that writes each interaction a dial creates to the audit log.</param>
    /// <param name="scopeExecutor">The executor used for compensation and post-commit command wake-up.</param>
    /// <param name="providerCommandStateService">The service used to persist provider command intent.</param>
    /// <param name="outboundLineResolver">The resolver of the line each agent dials out from.</param>
    /// <param name="addressManagers">The address list, which holds the number a load dials from, when the feature is on.</param>
    /// <param name="queueItemManager">The queue items, claimed by a call placed before any agent is reserved.</param>
    /// <param name="distributedLock">The lock that serializes claiming an activity with routing reserving it.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="coordinationOptions">The coordination options carrying the reservation lock timings.</param>
    /// <param name="logger">The logger instance.</param>
    public DialerAttemptService(
        IDialerEligibilityService eligibilityService,
        IActivityReservationService reservationService,
        IDialerAttemptCompensationService compensationService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IContactCenterWorkStateService workStateService,
        IContactCenterActivityWriter activityWriter,
        IAgentProfileManager agentManager,
        IVoiceContactCenterCallRouter voiceCallRouter,
        IContactCenterEventPublisher publisher,
        IContactCenterAuditRecorder auditRecorder,
        IContactCenterScopeExecutor scopeExecutor,
        IProviderCommandStateService providerCommandStateService,
        IOutboundLineResolver outboundLineResolver,
        IEnumerable<IOmnichannelChannelEndpointManager> addressManagers,
        IQueueItemManager queueItemManager,
        IDistributedLock distributedLock,
        IClock clock,
        IOptions<ContactCenterCoordinationOptions> coordinationOptions,
        ILogger<DialerAttemptService> logger)
        : this(
            eligibilityService,
            reservationService,
            compensationService,
            interactionManager,
            activityManager,
            workStateService,
            activityWriter,
            agentManager,
            voiceCallRouter,
            publisher,
            auditRecorder,
            scopeExecutor,
            providerCommandStateService,
            addressManagers,
            queueItemManager,
            distributedLock,
            clock,
            coordinationOptions,
            logger)
    {
        _outboundLineResolver = outboundLineResolver;
    }

    /// <inheritdoc/>
    public async Task<bool> TryDialAsync(DialerProfile profile, ActivityReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(reservation);

        var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

        if (activity is null)
        {
            await _compensationService.CompensateAsync(reservation, removeFromQueue: true, cancellationToken);

            return false;
        }

        var eligibility = await _eligibilityService.EvaluateAsync(new DialerEligibilityContext
        {
            Profile = profile,
            Activity = activity,
        }, cancellationToken);

        if (!eligibility.IsEligible)
        {
            await SuppressAsync(profile, reservation, activity, eligibility, cancellationToken);

            return false;
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        if (agent is null || string.IsNullOrWhiteSpace(agent.UserId))
        {
            _logger.LogWarning(
                "The dialer attempt for activity '{ActivityItemId}' failed closed because reserved agent '{AgentId}' could not be resolved to an Orchard user.",
                reservation.ActivityItemId.SanitizeLogValue(),
                reservation.AgentId.SanitizeLogValue());

            return false;
        }

        var acceptedReservation = await _reservationService.AcceptAsync(reservation.ItemId, cancellationToken);

        if (acceptedReservation is null)
        {
            return false;
        }

        try
        {
            await PlaceAsync(
                profile,
                activity,
                reservation.QueueId,
                agent,
                acceptedReservation.ItemId,
                await ResolveCallerIdAsync(profile, agent, activity, cancellationToken),
                cancellationToken);
        }
        catch
        {
            await _scopeExecutor.ExecuteAsync<IDialerAttemptCompensationService>(service =>
                service.CompensateAsync(acceptedReservation, removeFromQueue: true, CancellationToken.None));

            throw;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> TryDialUnreservedAsync(DialerProfile profile, QueueItem queueItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(queueItem);

        // The same lock routing takes to reserve the activity, so the dial and an agent offer can never both claim it.
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            ActivityReservationLockKeys.ForActivity(queueItem.ActivityItemId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration);

        if (!locked)
        {
            return false;
        }

        await using var acquiredLock = locker;

        var current = await _queueItemManager.FindByIdAsync(queueItem.ItemId, cancellationToken);

        if (current is null || current.Status != QueueItemStatus.Waiting)
        {
            return false;
        }

        var activity = await _activityManager.FindByIdAsync(current.ActivityItemId, cancellationToken);

        if (activity is null)
        {
            return false;
        }

        var eligibility = await _eligibilityService.EvaluateAsync(new DialerEligibilityContext
        {
            Profile = profile,
            Activity = activity,
        }, cancellationToken);

        if (!eligibility.IsEligible)
        {
            await SuppressUnreservedAsync(profile, current, activity, eligibility, cancellationToken);

            return false;
        }

        var now = _clock.UtcNow;

        // The call is placed for the campaign, not for an agent: nobody is reserved, the item is Assigned with no agent,
        // and the first person who answers is connected to whichever agent is free then.
        current.TransitionTo(QueueItemStatus.Assigned);
        current.AgentId = null;
        current.ReservationId = null;
        current.DialedUtc = now;
        await _queueItemManager.UpdateAsync(current, cancellationToken: cancellationToken);

        await _workStateService.MutateAsync(activity.ItemId, workState =>
        {
            workState.TransitionTo(ActivityAssignmentStatus.Assigned);
            workState.ReservationId = null;
            workState.ReservedById = null;
            workState.ReservedByUsername = null;
            workState.ReservedUtc = null;
            workState.ReservationExpiresUtc = null;
            workState.AssignedToId = null;
            workState.AssignedToUsername = null;
            workState.AssignedToUtc = now;
        }, cancellationToken);

        await PlaceAsync(
            profile,
            activity,
            current.QueueId,
            agent: null,
            reservationId: null,
            await ResolveCallerIdAsync(profile, agent: null, activity, cancellationToken),
            cancellationToken);

        return true;
    }

    // Records the attempt and registers the provider dial, which is dispatched once the caller's transaction commits: a
    // commit that is lost places no call. A call placed without an agent carries no agent on the request, on the
    // interaction or on the command, and is marked as over-dialed so the answer knows to pick an agent for it.
    private async Task PlaceAsync(
        DialerProfile profile,
        OmnichannelActivity activity,
        string queueId,
        AgentProfile agent,
        string reservationId,
        string callerId,
        CancellationToken cancellationToken)
    {
        var interaction = await _interactionManager.NewAsync(cancellationToken: cancellationToken);
        interaction.Channel = InteractionChannel.Voice;
        interaction.Direction = InteractionDirection.Outbound;
        interaction.TransitionTo(InteractionStatus.Created);
        interaction.ActivityItemId = activity.ItemId;
        interaction.QueueId = queueId;
        interaction.AgentId = agent?.ItemId;
        interaction.ProviderName = _voiceCallRouter.GetOutboundProviderName(profile.ProviderName);
        interaction.CustomerAddress = activity.PreferredDestination;
        interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId] = interaction.ItemId;
        var request = new ContactCenterDialRequest
        {
            ActivityId = activity.ItemId,
            InteractionId = interaction.ItemId,
            CommandId = interaction.ItemId,
            AgentId = agent?.ItemId,
            AgentUserId = agent?.UserId,
            QueueId = queueId,
            CampaignId = activity.CampaignId,
            Destination = activity.PreferredDestination,
            CallerId = callerId,
            Metadata = new Dictionary<string, string>
            {
                [ContactCenterConstants.CommandMetadata.CommandId] = interaction.ItemId,
                [TelephonyConstants.RequestMetadata.IdempotencyKey] = interaction.ItemId,
            },
        };

        // Only a paced call is screened: in preview the agent placed the call and is already listening for who answers.
        if (profile.Mode.IsAutomated() && profile.AnsweringMachineDetection != DialerAnsweringMachineDetection.Disabled)
        {
            request.Metadata[TelephonyConstants.RequestMetadata.AnsweringMachineDetection] =
                profile.AnsweringMachineDetection == DialerAnsweringMachineDetection.Premium ? "premium" : "standard";
        }

        // A paced call rings for the profile's ring time, never less than the fifteen seconds the abandoned-call rules
        // expect, whatever an imported profile says. A preview call is the agent's, who hangs up when they choose.
        if (profile.Mode.IsAutomated())
        {
            request.Metadata[TelephonyConstants.RequestMetadata.RingTimeoutSeconds] =
                DialerAbandonment.ResolveRingTimeoutSeconds(profile).ToString(CultureInfo.InvariantCulture);
        }

        // The activity's first call is the attempt the activity already stands for (a follow-up created to try
        // again starts at the attempt after the one it follows); only a further call of the same activity is a
        // new attempt. Counting every call as one made a three-attempt profile stop after two calls.
        var attemptNumber = 1;

        await _workStateService.MutateAsync(
            activity.ItemId,
            workState =>
            {
                attemptNumber = ContactCenterWorkState.NextAttemptNumber(workState, activity.Attempts);
                workState.Attempts = attemptNumber;
                workState.DialCount++;
            },
            cancellationToken);

        DialerCallMetadata.StampDial(interaction, profile, attemptNumber);

        if (agent is null)
        {
            DialerCallMetadata.MarkOverDialed(interaction);
        }

        await _interactionManager.CreateAsync(interaction, cancellationToken: cancellationToken);
        await _auditRecorder.RecordInteractionCreatedAsync(interaction, activity.Source, ContactCenterActor.System, cancellationToken);
        await _publisher.PublishAsync(new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.DialerAttemptStarted,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(DialerProfile),
            AggregateId = profile.ItemId,
            SourceComponent = ContactCenterConstants.Components.Dialer,
            IdempotencyKey = $"dialer-attempt:{interaction.ItemId}",
        }, cancellationToken);
        await _providerCommandStateService.RegisterAsync(new ProviderCommandRegistration
        {
            CommandId = interaction.ItemId,
            ProviderName = interaction.ProviderName,
            CommandType = ProviderCommandType.Dial,
            ActivityItemId = activity.ItemId,
            InteractionId = interaction.ItemId,
            ReservationId = reservationId,
            DialerProfileId = profile.ItemId,
            RequestPayload = JsonSerializer.Serialize(request),
        }, cancellationToken);

        _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
            processor.DispatchAsync(interaction.ItemId, CancellationToken.None));
    }

    // The number the customer sees. A profile that insists on its own caller ID wins. Next comes the number picked when
    // the activities were loaded, which is a choice made for exactly these calls. Then the agent's own line, so a
    // customer who calls back reaches the agent who called them; then the profile's caller ID; and with none of them
    // the provider presents its default. A number that cannot be read never stops the attempt.
    private async Task<string> ResolveCallerIdAsync(DialerProfile profile, AgentProfile agent, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (profile.AlwaysUseCallerId && !string.IsNullOrWhiteSpace(profile.CallerId))
        {
            return profile.CallerId;
        }

        var loadNumber = await ResolveLoadNumberAsync(activity, cancellationToken);

        if (!string.IsNullOrWhiteSpace(loadNumber))
        {
            return loadNumber;
        }

        // A call placed before any agent is reserved has no agent line to present: nobody knows yet who takes it.
        if (_outboundLineResolver is null || agent is null)
        {
            return profile.CallerId;
        }

        try
        {
            var line = await _outboundLineResolver.ResolveAsync(agent.UserId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(line?.Number))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Dialer profile '{Profile}' presents agent '{AgentId}''s outbound line {LineId} ({LineNumber}) instead of the profile caller ID.",
                        profile.Name,
                        agent.ItemId.SanitizeLogValue(),
                        line.Id,
                        line.Number);
                }

                return line.Number;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "The outbound line of agent '{AgentId}' could not be read; dialer profile '{Profile}' presents its own caller ID instead.",
                agent.ItemId.SanitizeLogValue(),
                profile.Name);
        }

        return profile.CallerId;
    }

    private async Task<string> ResolveLoadNumberAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (_addressManager is null || string.IsNullOrWhiteSpace(activity.ChannelEndpointId))
        {
            return null;
        }

        try
        {
            var address = await _addressManager.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

            // A number no longer used for calls is not presented; the call falls back as if none had been picked.
            return address is not null && address.HasCapability(OmnichannelConstants.Channels.Phone) ? address.Value : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The number picked for activity '{ActivityItemId}' could not be read; the call falls back to the agent's line or the profile caller ID.", activity.ItemId.SanitizeLogValue());

            return null;
        }
    }

    // An over-dialed attempt the compliance gate refuses is handled as a reserved one would be, without a reservation to
    // release: a record that may never be dialed leaves the queue, one that may be dialed later stays Waiting.
    private async Task SuppressUnreservedAsync(
        DialerProfile profile,
        QueueItem queueItem,
        OmnichannelActivity activity,
        DialerEligibilityResult eligibility,
        CancellationToken cancellationToken)
    {
        var status = ResolveSuppressedStatus(eligibility.Reason);

        if (eligibility.Reason == DialerSuppressionReason.MaxAttemptsReached &&
            !QueueCallbackDialerProfile.IsCallbackProfile(profile.ItemId) &&
            ScheduleExhaustedFinalization(activity.ItemId))
        {
            await RemoveFromQueueAsync(queueItem, cancellationToken);
            await PublishSuppressedAsync(profile, activity, eligibility, cancellationToken);

            return;
        }

        if (status.HasValue)
        {
            var terminalReasonCode = eligibility.Reason == DialerSuppressionReason.NumberNotInService
                ? OmnichannelConstants.TerminalReasons.NumberNotInService
                : null;

            await _activityWriter.ScheduleUpdateAsync(
                activity.ItemId,
                suppressed =>
                {
                    suppressed.Status = status.Value;

                    if (terminalReasonCode is not null)
                    {
                        suppressed.TerminalReasonCode = terminalReasonCode;
                    }
                },
                cancellationToken);

            await RemoveFromQueueAsync(queueItem, cancellationToken);
        }

        await PublishSuppressedAsync(profile, activity, eligibility, cancellationToken);
    }

    private async Task RemoveFromQueueAsync(QueueItem queueItem, CancellationToken cancellationToken)
    {
        queueItem.TransitionTo(QueueItemStatus.Removed);
        queueItem.DequeuedUtc = _clock.UtcNow;
        await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

        await _workStateService.MutateAsync(queueItem.ActivityItemId, workState =>
        {
            if (workState.AssignmentStatus != ActivityAssignmentStatus.Released && workState.CanTransitionTo(ActivityAssignmentStatus.Released))
            {
                workState.TransitionTo(ActivityAssignmentStatus.Released);
            }
        }, cancellationToken);
    }

    private async Task SuppressAsync(
        DialerProfile profile,
        ActivityReservation reservation,
        OmnichannelActivity activity,
        DialerEligibilityResult eligibility,
        CancellationToken cancellationToken)
    {
        var status = ResolveSuppressedStatus(eligibility.Reason);

        // A record that has used every attempt is finished by the dialer with the disposition for how its last attempt
        // ended, like any attempt that never reached an agent, rather than left failed with nothing for a workflow or a
        // report to act on. It is completed once this cycle has committed and the reservation is released.
        if (eligibility.Reason == DialerSuppressionReason.MaxAttemptsReached &&
            !QueueCallbackDialerProfile.IsCallbackProfile(profile.ItemId) &&
            ScheduleExhaustedFinalization(activity.ItemId))
        {
            await _compensationService.CompensateAsync(reservation, removeFromQueue: true, cancellationToken);
            await PublishSuppressedAsync(profile, activity, eligibility, cancellationToken);

            return;
        }

        if (status.HasValue)
        {
            // A number already known dead is recorded as the reason the attempt was never made, so a report can
            // tell these apart from attempts that were dialed and found the number out of service.
            var terminalReasonCode = eligibility.Reason == DialerSuppressionReason.NumberNotInService
                ? OmnichannelConstants.TerminalReasons.NumberNotInService
                : null;

            await _activityWriter.ScheduleUpdateAsync(
                activity.ItemId,
                suppressed =>
                {
                    suppressed.Status = status.Value;

                    if (terminalReasonCode is not null)
                    {
                        suppressed.TerminalReasonCode = terminalReasonCode;
                    }
                },
                cancellationToken);
        }

        await _compensationService.CompensateAsync(reservation, removeFromQueue: status.HasValue, cancellationToken);
        await PublishSuppressedAsync(profile, activity, eligibility, cancellationToken);
    }

    private bool ScheduleExhaustedFinalization(string activityItemId)
        => _scopeExecutor.ScheduleAfterCommit<IServiceProvider>(services =>
            DialerAttemptFinalizer.FinalizeExhaustedAsync(services, activityItemId));

    private async Task PublishSuppressedAsync(
        DialerProfile profile,
        OmnichannelActivity activity,
        DialerEligibilityResult eligibility,
        CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Suppressed outbound attempt for activity '{ActivityItemId}' on profile '{Profile}': {Reason}.",
                activity.ItemId.SanitizeLogValue(),
                profile.Name,
                eligibility.Reason);
        }

        var data = new DialerSuppressionEventData
        {
            ProfileItemId = profile.ItemId,
            ActivityItemId = activity.ItemId,
            Reason = eligibility.Reason,
            Description = eligibility.Description,
            Destination = activity.PreferredDestination,
        };

        var suppressionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.DialSuppressed,
            AggregateType = nameof(OmnichannelActivity),
            AggregateId = activity.ItemId,
            SourceComponent = ContactCenterConstants.Components.Dialer,
        };

        suppressionEvent.SetData(data);

        await _publisher.PublishAsync(suppressionEvent, cancellationToken);
    }

    private static ActivityStatus? ResolveSuppressedStatus(DialerSuppressionReason reason)
    {
        return reason switch
        {
            DialerSuppressionReason.NoDestination => ActivityStatus.Failed,
            DialerSuppressionReason.MaxAttemptsReached => ActivityStatus.Failed,
            DialerSuppressionReason.DoNotCall => ActivityStatus.Cancelled,
            DialerSuppressionReason.NationalDoNotCallRegistry => ActivityStatus.Cancelled,
            DialerSuppressionReason.NumberNotInService => ActivityStatus.Cancelled,
            _ => null,
        };
    }
}
