using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IVoiceContactCenterCallRouter"/> implementation. It routes inbound voice calls
/// into CRM activities and outbound voice dial requests to provider implementations while Telephony
/// remains responsible for media execution.
/// </summary>
public sealed class VoiceContactCenterCallRouter : IVoiceContactCenterCallRouter, IInboundVoiceService
{
    private const string ServiceAddressMetadataKey = "serviceAddress";
    private const int MaxOfferAttempts = 25;
    private static readonly TimeSpan _inboundLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _inboundLockExpiration = TimeSpan.FromMinutes(1);

    private readonly IOmnichannelChannelEndpointManager _channelEndpointManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContentManager _contentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueService _queueService;
    private readonly IActivityAssignmentService _assignmentService;
    private readonly IActivityReservationService _reservationService;
    private readonly IAgentProfileManager _agentManager;
    private readonly IInboundContactLookup _contactLookup;
    private readonly IIncomingCallDispatcher _incomingCallDispatcher;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IEntryPointResolver _entryPointResolver;
    private readonly IProviderCallStateSynchronizationService _providerCallStateSynchronizationService;
    private readonly IProviderVoiceOfferSynchronizationService _offerSynchronizationService;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceContactCenterCallRouter"/> class.
    /// </summary>
    /// <param name="channelEndpointManager">The channel endpoint manager used to map the dialed number to an endpoint.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service used to resolve the subject and campaign.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="contentManager">The content manager used to create the subject and load contacts.</param>
    /// <param name="interactionManager">The interaction manager used to record communication history.</param>
    /// <param name="queueManager">The queue manager used to resolve the inbound queue.</param>
    /// <param name="queueItemManager">The queue item manager used to determine the current queue state of an existing call.</param>
    /// <param name="queueService">The queue service used to enqueue the activity.</param>
    /// <param name="assignmentService">The assignment service used to reserve an available agent.</param>
    /// <param name="reservationService">The reservation service used to release invalid offers.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the reserved agent.</param>
    /// <param name="contactLookup">The contact lookup used to resolve the caller.</param>
    /// <param name="incomingCallDispatcher">The dispatcher used to offer the ringing call to the agent.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used for outbound voice calls.</param>
    /// <param name="entryPointResolver">The entry point resolver used to route inbound calls by dialed number.</param>
    /// <param name="providerCallStateSynchronizationService">The provider call-state synchronization service used to confirm a queued call still exists before it is offered.</param>
    /// <param name="offerSynchronizationService">The offer synchronization service used to remove queued calls that no longer exist on the provider.</param>
    /// <param name="distributedLock">The distributed lock used to serialize inbound call creation by provider call id.</param>
    /// <param name="session">The YesSql session used to persist queue changes before selecting the next call.</param>
    /// <param name="clock">The clock used to stamp times.</param>
    public VoiceContactCenterCallRouter(
        IOmnichannelChannelEndpointManager channelEndpointManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IOmnichannelActivityManager activityManager,
        IContentManager contentManager,
        IInteractionManager interactionManager,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IActivityQueueService queueService,
        IActivityAssignmentService assignmentService,
        IActivityReservationService reservationService,
        IAgentProfileManager agentManager,
        IInboundContactLookup contactLookup,
        IIncomingCallDispatcher incomingCallDispatcher,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IEntryPointResolver entryPointResolver,
        IProviderCallStateSynchronizationService providerCallStateSynchronizationService,
        IProviderVoiceOfferSynchronizationService offerSynchronizationService,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock)
    {
        _channelEndpointManager = channelEndpointManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _activityManager = activityManager;
        _contentManager = contentManager;
        _interactionManager = interactionManager;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _queueService = queueService;
        _assignmentService = assignmentService;
        _reservationService = reservationService;
        _agentManager = agentManager;
        _contactLookup = contactLookup;
        _incomingCallDispatcher = incomingCallDispatcher;
        _voiceProviderResolver = voiceProviderResolver;
        _entryPointResolver = entryPointResolver;
        _providerCallStateSynchronizationService = providerCallStateSynchronizationService;
        _offerSynchronizationService = offerSynchronizationService;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public bool CanRouteOutbound(string providerName = null)
    {
        var provider = _voiceProviderResolver.Get(providerName);

        return provider?.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.DialerDial) == true;
    }

    /// <inheritdoc/>
    public string GetOutboundProviderName(string providerName = null)
    {
        return _voiceProviderResolver.Get(providerName)?.TechnicalName;
    }

    /// <inheritdoc/>
    public Task<InboundVoiceRoutingResult> HandleInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default)
    {
        return RouteInboundAsync(inboundEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ContactCenterVoiceProviderResult> RouteOutboundAsync(
        ContactCenterDialRequest request,
        string providerName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = _voiceProviderResolver.Get(providerName);

        if (provider is null)
        {
            return Failure("provider_unavailable", "No Contact Center voice provider is registered for outbound voice routing.");
        }

        if (!provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.DialerDial))
        {
            return Failure("dialing_not_supported", "The Contact Center voice provider does not support outbound dialing.");
        }

        var result = await provider.DialAsync(request, cancellationToken);

        return result ?? Failure("provider_returned_no_result", "The Contact Center voice provider did not return a result.");
    }

    /// <inheritdoc/>
    public async Task<InboundVoiceRoutingResult> RouteInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);

        if (string.IsNullOrWhiteSpace(inboundEvent.ProviderCallId))
        {
            return await RouteInboundCoreAsync(inboundEvent, cancellationToken);
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetInboundLockKey(inboundEvent),
            _inboundLockTimeout,
            _inboundLockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"Inbound call '{inboundEvent.ProviderCallId}' is already being routed.");
        }

        var releaseDeferred = false;

        try
        {
            if (ShellScope.Current is not null)
            {
                ShellScope.AddDeferredTask(_ => locker.DisposeAsync().AsTask());
                releaseDeferred = true;
            }

            return await RouteInboundCoreAsync(inboundEvent, cancellationToken);
        }
        finally
        {
            if (!releaseDeferred && locker is not null)
            {
                await locker.DisposeAsync();
            }
        }
    }

    private async Task<InboundVoiceRoutingResult> RouteInboundCoreAsync(
        InboundVoiceEvent inboundEvent,
        CancellationToken cancellationToken)
    {
        var result = new InboundVoiceRoutingResult();
        Interaction existing = null;

        if (!string.IsNullOrWhiteSpace(inboundEvent.ProviderCallId))
        {
            existing = !string.IsNullOrWhiteSpace(inboundEvent.ProviderName)
                ? await _interactionManager.FindByProviderInteractionIdAsync(
                    inboundEvent.ProviderName,
                    inboundEvent.ProviderCallId,
                    cancellationToken)
                : await _interactionManager.FindByProviderInteractionIdAsync(inboundEvent.ProviderCallId, cancellationToken);
        }

        if (existing is not null)
        {
            var queueItem = !string.IsNullOrEmpty(existing.ActivityItemId)
                ? await _queueItemManager.FindByActivityIdAsync(existing.ActivityItemId, cancellationToken)
                : null;

            result.ActivityItemId = existing.ActivityItemId;
            result.InteractionId = existing.ItemId;
            result.QueueId = queueItem?.QueueId ?? existing.QueueId;
            result.Routed = !string.IsNullOrEmpty(existing.AgentId) ||
                queueItem?.Status is QueueItemStatus.Reserved or QueueItemStatus.Assigned;
            result.Queued = queueItem?.Status == QueueItemStatus.Waiting;
            result.Reason = "The provider call is already tracked by the Contact Center.";

            return result;
        }

        var now = inboundEvent.ReceivedUtc ?? _clock.UtcNow;
        var fromAddress = inboundEvent.FromAddress?.GetCleanedPhoneNumber();
        var serviceAddress = inboundEvent.ToAddress?.GetCleanedPhoneNumber();

        if (string.IsNullOrEmpty(serviceAddress))
        {
            result.Reason = "The inbound call did not include a destination address.";

            return result;
        }

        var endpoint = await _channelEndpointManager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Phone, serviceAddress, cancellationToken);

        var flow = await ResolveFlowAsync(endpoint, cancellationToken);

        var contactItemId = await ResolveContactAsync(fromAddress, cancellationToken);

        var activity = await CreateActivityAsync(endpoint, flow, fromAddress, contactItemId, now);
        result.ActivityItemId = activity.ItemId;

        var plan = await _entryPointResolver.ResolveAsync(serviceAddress, cancellationToken);

        var queue = plan is not null && !string.IsNullOrEmpty(plan.TargetQueueId)
            ? await _queueManager.FindByIdAsync(plan.TargetQueueId, cancellationToken)
            : await ResolveQueueAsync(endpoint, cancellationToken);

        var interaction = await CreateInteractionAsync(inboundEvent, activity, queue, fromAddress, serviceAddress);
        result.InteractionId = interaction.ItemId;

        if (plan is not null && !plan.ShouldQueue)
        {
            result.Reason = plan.ClosedAction == EntryPointClosedAction.Voicemail
                ? "The entry point is closed; the caller was routed to voicemail."
                : "The entry point is closed; the call was rejected.";

            return result;
        }

        if (queue is null)
        {
            result.Reason = "No inbound queue is configured to receive this call.";

            return result;
        }

        result.QueueId = queue.ItemId;

        var priority = plan is not null ? plan.Priority : (InteractionPriority?)null;

        await _queueService.EnqueueAsync(activity.ItemId, queue.ItemId, priority, cancellationToken);
        result.Queued = true;

        var agentUserId = await OfferNextAsync(queue.ItemId, cancellationToken);

        if (string.IsNullOrEmpty(agentUserId))
        {
            result.Reason = "The call is waiting in the queue for the next eligible agent.";

            return result;
        }

        result.Routed = true;
        result.Queued = false;
        result.AgentUserId = agentUserId;
        result.Reason = "Offered to an available agent.";

        return result;
    }

    /// <inheritdoc/>
    public async Task<string> OfferNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        // Offer the next viable queued call. The telephony provider is the source of truth, so any queued
        // call whose provider channel no longer exists is removed instead of being offered. Skipping such
        // "zombie" calls prevents an agent from being reserved for a call they can never answer or hang up,
        // which would otherwise leave them stuck and unable to receive new inbound calls. The loop is bounded
        // to avoid spinning if reservations keep failing for unrelated reasons.
        for (var attempt = 0; attempt < MaxOfferAttempts; attempt++)
        {
            var reservation = await _assignmentService.AssignNextAsync(queueId, cancellationToken);

            if (reservation is null)
            {
                return null;
            }

            var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

            if (agent is null || string.IsNullOrEmpty(agent.UserId))
            {
                await _reservationService.RejectAsync(reservation.ItemId, cancellationToken);

                return null;
            }

            var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

            if (interaction is null)
            {
                var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

                if (activity is not null &&
                    !string.Equals(activity.Source, ActivitySources.Inbound, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                await _reservationService.RejectAsync(reservation.ItemId, cancellationToken);

                return null;
            }

            interaction = await _providerCallStateSynchronizationService.RefreshInteractionAsync(interaction, cancellationToken);

            if (interaction.Status is InteractionStatus.Ended or InteractionStatus.Failed)
            {
                // Provider truth reports the call no longer exists. Remove it from the queue and release the
                // reservation and agent so the dead call is never offered again, then try the next call.
                await _offerSynchronizationService.ReconcileEndedOfferAsync(interaction.ItemId, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);

                continue;
            }

            interaction.Status = InteractionStatus.Ringing;
            interaction.AgentId = agent.ItemId;
            interaction.QueueId = reservation.QueueId;
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

            var call = new TelephonyCall
            {
                CallId = interaction.ProviderInteractionId,
                From = interaction.CustomerAddress,
                To = ResolveServiceAddress(interaction),
                State = CallState.Ringing,
                Direction = CallDirection.Inbound,
                ProviderName = interaction.ProviderName,
                StartedUtc = _clock.UtcNow,
                Metadata = BuildCallMetadata(interaction),
            };

            await _incomingCallDispatcher.DispatchAsync(agent.UserId, call, cancellationToken);

            return agent.UserId;
        }

        return null;
    }

    private static string ResolveServiceAddress(Core.Models.Interaction interaction)
    {
        return interaction.TechnicalMetadata.TryGetValue(ServiceAddressMetadataKey, out var value)
            ? value?.ToString()
            : null;
    }

    private static Dictionary<string, object> BuildCallMetadata(Core.Models.Interaction interaction)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(interaction.CustomerAddress))
        {
            metadata["callerAddress"] = interaction.CustomerAddress;
        }

        var serviceAddress = ResolveServiceAddress(interaction);

        if (!string.IsNullOrWhiteSpace(serviceAddress))
        {
            metadata["calledAddress"] = serviceAddress;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ProviderName))
        {
            metadata["providerName"] = interaction.ProviderName;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ItemId))
        {
            metadata["interactionId"] = interaction.ItemId;
        }

        if (!string.IsNullOrWhiteSpace(interaction.ActivityItemId))
        {
            metadata["activityItemId"] = interaction.ActivityItemId;
        }

        if (!string.IsNullOrWhiteSpace(interaction.QueueId))
        {
            metadata["queueId"] = interaction.QueueId;
        }

        return metadata;
    }

    private static ContactCenterVoiceProviderResult Failure(string errorCode, string errorMessage)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };
    }

    private async Task<SubjectFlowSettings> ResolveFlowAsync(OmnichannelChannelEndpoint endpoint, CancellationToken cancellationToken)
    {
        if (endpoint is null)
        {
            return null;
        }

        var flows = await _subjectFlowSettingsService.GetConfiguredFlowSettingsAsync(cancellationToken);

        return flows.FirstOrDefault(flow =>
            string.Equals(flow.Channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(flow.ChannelEndpointId, endpoint.ItemId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> ResolveContactAsync(string fromAddress, CancellationToken cancellationToken)
    {
        var contactIds = await _contactLookup.FindContactItemIdsAsync(fromAddress, cancellationToken);

        return contactIds.Count > 0 ? contactIds[0] : null;
    }

    private async Task<OmnichannelActivity> CreateActivityAsync(
        OmnichannelChannelEndpoint endpoint,
        SubjectFlowSettings flow,
        string fromAddress,
        string contactItemId,
        DateTime now)
    {
        var activity = await _activityManager.NewAsync();
        activity.Kind = ActivityKind.Call;
        activity.Source = ActivitySources.Inbound;
        activity.Channel = OmnichannelConstants.Channels.Phone;
        activity.ChannelEndpointId = endpoint?.ItemId;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.PreferredDestination = fromAddress;
        activity.CampaignId = flow?.CampaignId;
        activity.SubjectContentType = flow?.SubjectContentType;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        activity.Status = ActivityStatus.AwaitingAgentResponse;
        activity.ScheduledUtc = now;
        activity.CreatedUtc = now;

        if (!string.IsNullOrEmpty(contactItemId))
        {
            var contact = await _contentManager.GetAsync(contactItemId);

            if (contact is not null)
            {
                activity.ContactContentItemId = contact.ContentItemId;
                activity.ContactContentType = contact.ContentType;
            }
        }

        if (!string.IsNullOrEmpty(activity.SubjectContentType))
        {
            activity.Subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        await _activityManager.CreateAsync(activity);

        return activity;
    }

    private async Task<Core.Models.Interaction> CreateInteractionAsync(
        InboundVoiceEvent inboundEvent,
        OmnichannelActivity activity,
        ActivityQueue queue,
        string fromAddress,
        string serviceAddress)
    {
        var interaction = await _interactionManager.NewAsync();
        interaction.Channel = InteractionChannel.Voice;
        interaction.Direction = InteractionDirection.Inbound;
        interaction.Status = InteractionStatus.Created;
        interaction.ActivityItemId = activity.ItemId;
        interaction.ProviderName = inboundEvent.ProviderName;
        interaction.ProviderInteractionId = inboundEvent.ProviderCallId;
        interaction.CustomerAddress = fromAddress;
        interaction.QueueId = queue?.ItemId;

        if (!string.IsNullOrEmpty(serviceAddress))
        {
            interaction.TechnicalMetadata[ServiceAddressMetadataKey] = serviceAddress;
        }

        foreach (var entry in inboundEvent.Metadata)
        {
            interaction.TechnicalMetadata[entry.Key] = entry.Value;
        }

        await _interactionManager.CreateAsync(interaction);

        return interaction;
    }

    private static string GetInboundLockKey(InboundVoiceEvent inboundEvent)
    {
        return $"ContactCenterInboundVoice:{inboundEvent.ProviderName}:{inboundEvent.ProviderCallId}";
    }

    private async Task<ActivityQueue> ResolveQueueAsync(OmnichannelChannelEndpoint endpoint, CancellationToken cancellationToken)
    {
        var queues = await _queueManager.ListEnabledAsync(cancellationToken);

        if (endpoint is not null)
        {
            var mapped = queues.FirstOrDefault(queue =>
                string.Equals(queue.InboundChannelEndpointId, endpoint.ItemId, StringComparison.OrdinalIgnoreCase));

            if (mapped is not null)
            {
                return mapped;
            }
        }

        var unmapped = queues
            .Where(queue => string.IsNullOrEmpty(queue.InboundChannelEndpointId))
            .ToList();

        return unmapped.Count == 1 ? unmapped[0] : null;
    }
}
