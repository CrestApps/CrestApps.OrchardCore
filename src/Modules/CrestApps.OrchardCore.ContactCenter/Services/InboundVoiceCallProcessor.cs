using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IInboundVoiceCallProcessor"/> implementation. It routes inbound voice calls into CRM
/// activities and interactions while Telephony remains responsible for media execution.
/// </summary>
public sealed class InboundVoiceCallProcessor : IInboundVoiceCallProcessor
{
    private const string ServiceAddressMetadataKey = "serviceAddress";
    private const string RoutingTerminalReasonMetadataKey = "routing_terminal_reason";
    private const string ClosedVoicemailReasonCode = "entry_point_closed_voicemail";
    private const string ClosedRejectReasonCode = "entry_point_closed_reject";
    private const string TargetQueueMissingReasonCode = "target_queue_missing";
    private const string TargetQueueDisabledReasonCode = "target_queue_disabled";
    private const string InboundQueueUnavailableReasonCode = "inbound_queue_unavailable";
    private readonly IOmnichannelChannelEndpointManager _channelEndpointManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IContactCenterActivityWriter _activityWriter;
    private readonly IContentManager _contentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueService _queueService;
    private readonly IInboundContactLookup _contactLookup;
    private readonly IEnumerable<IEntryPointResolver> _entryPointResolvers;
    private readonly IProviderCommandStateService _providerCommandStateService;
    private readonly IVoiceQueueOfferService _offerService;
    private readonly IDistributedLock _distributedLock;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IClock _clock;
    private readonly TimeSpan _inboundLockTimeout;
    private readonly TimeSpan _inboundLockExpiration;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundVoiceCallProcessor"/> class.
    /// </summary>
    /// <param name="channelEndpointManager">The channel endpoint manager used to map the dialed number to an endpoint.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service used to resolve the subject and campaign.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="activityWriter">The writer used to apply CRM activity changes outside the routing transaction.</param>
    /// <param name="contentManager">The content manager used to create the subject and load contacts.</param>
    /// <param name="interactionManager">The interaction manager used to record communication history.</param>
    /// <param name="queueManager">The queue manager used to resolve the inbound queue.</param>
    /// <param name="queueItemManager">The queue item manager used to determine the current queue state of an existing call.</param>
    /// <param name="queueService">The queue service used to enqueue the activity.</param>
    /// <param name="contactLookup">The contact lookup used to resolve the caller.</param>
    /// <param name="entryPointResolvers">The optional entry point resolvers used to route inbound calls by dialed number.</param>
    /// <param name="providerCommandStateService">The service used to persist provider actions before dispatch.</param>
    /// <param name="offerService">The offer service used to reserve and offer the call to an available agent.</param>
    /// <param name="distributedLock">The distributed lock used to serialize inbound call creation by provider call id.</param>
    /// <param name="scopeExecutor">The executor used to release inbound routing locks after commit.</param>
    /// <param name="workManager">The feature work manager used to reject routing while Voice is quiescing.</param>
    /// <param name="clock">The clock used to stamp times.</param>
    /// <param name="coordinationOptions">The distributed-lock timings this deployment coordinates inbound routing with.</param>
    public InboundVoiceCallProcessor(
        IOmnichannelChannelEndpointManager channelEndpointManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IOmnichannelActivityManager activityManager,
        IContactCenterWorkStateService workStateService,
        IContactCenterActivityWriter activityWriter,
        IContentManager contentManager,
        IInteractionManager interactionManager,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IActivityQueueService queueService,
        IInboundContactLookup contactLookup,
        IEnumerable<IEntryPointResolver> entryPointResolvers,
        IProviderCommandStateService providerCommandStateService,
        IVoiceQueueOfferService offerService,
        IDistributedLock distributedLock,
        IContactCenterScopeExecutor scopeExecutor,
        IContactCenterFeatureWorkManager workManager,
        IClock clock,
        IOptions<ContactCenterCoordinationOptions> coordinationOptions)
    {
        _channelEndpointManager = channelEndpointManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _activityManager = activityManager;
        _workStateService = workStateService;
        _activityWriter = activityWriter;
        _contentManager = contentManager;
        _interactionManager = interactionManager;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _queueService = queueService;
        _contactLookup = contactLookup;
        _entryPointResolvers = entryPointResolvers;
        _providerCommandStateService = providerCommandStateService;
        _offerService = offerService;
        _distributedLock = distributedLock;
        _scopeExecutor = scopeExecutor;
        _workManager = workManager;
        _clock = clock;
        _inboundLockTimeout = coordinationOptions.Value.InboundLockTimeout;
        _inboundLockExpiration = coordinationOptions.Value.InboundLockExpiration;
    }

    /// <inheritdoc/>
    public async Task<InboundVoiceRoutingResult> RouteInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);

        using var workLease = _workManager.TryEnter(ContactCenterConstants.Feature.Voice);

        if (workLease is null)
        {
            return new InboundVoiceRoutingResult
            {
                Reason = "The Contact Center Voice feature is temporarily unavailable.",
            };
        }

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
            if (_scopeExecutor.ScheduleAfterCommit(() => locker.DisposeAsync().AsTask()))
            {
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

            if (existing.TechnicalMetadata.TryGetValue(RoutingTerminalReasonMetadataKey, out var reasonCode))
            {
                result.ReasonCode = reasonCode as string;
            }

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

        var contactItemIds = await ResolveContactsAsync(fromAddress, cancellationToken);

        var entryPointResolver = _entryPointResolvers.FirstOrDefault();
        var plan = entryPointResolver is null
            ? null
            : await entryPointResolver.ResolveAsync(serviceAddress, cancellationToken);
        ActivityQueue queue = null;
        string unavailableQueueReasonCode = null;

        if (plan is not null)
        {
            if (plan.ShouldQueue)
            {
                if (string.IsNullOrEmpty(plan.TargetQueueId))
                {
                    unavailableQueueReasonCode = TargetQueueMissingReasonCode;
                }
                else
                {
                    queue = await _queueManager.FindByIdAsync(plan.TargetQueueId, cancellationToken);

                    if (queue is null)
                    {
                        unavailableQueueReasonCode = TargetQueueMissingReasonCode;
                    }
                    else if (!queue.Enabled)
                    {
                        queue = null;
                        unavailableQueueReasonCode = TargetQueueDisabledReasonCode;
                    }
                }
            }
        }
        else
        {
            queue = await ResolveQueueAsync(endpoint, cancellationToken);

            if (queue is null)
            {
                unavailableQueueReasonCode = InboundQueueUnavailableReasonCode;
            }
        }

        var activity = await CreateActivityAsync(endpoint, flow, fromAddress, contactItemIds, now);
        result.ActivityItemId = activity.ItemId;

        var interaction = await CreateInteractionAsync(inboundEvent, activity, queue, fromAddress, serviceAddress);
        result.InteractionId = interaction.ItemId;

        if (plan is not null && !plan.ShouldQueue)
        {
            var isVoicemail = plan.ClosedAction == EntryPointClosedAction.Voicemail;
            var reasonCode = isVoicemail
                ? ClosedVoicemailReasonCode
                : ClosedRejectReasonCode;
            result.Reason = isVoicemail
                ? "The entry point is closed and configured for voicemail handling."
                : "The entry point is closed and configured to reject the call.";
            result.ReasonCode = reasonCode;

            await TerminalizeInboundAsync(
                activity,
                interaction,
                isVoicemail ? ActivityStatus.Completed : ActivityStatus.Cancelled,
                InteractionStatus.Ended,
                reasonCode,
                isVoicemail ? ProviderCommandType.SendToVoicemail : ProviderCommandType.Reject,
                now,
                cancellationToken);

            return result;
        }

        if (queue is null)
        {
            result.Reason = unavailableQueueReasonCode switch
            {
                TargetQueueMissingReasonCode => "The entry point target queue does not exist.",
                TargetQueueDisabledReasonCode => "The entry point target queue is disabled.",
                _ => "No inbound queue is configured to receive this call.",
            };
            result.ReasonCode = unavailableQueueReasonCode ?? InboundQueueUnavailableReasonCode;

            await TerminalizeInboundAsync(
                activity,
                interaction,
                ActivityStatus.Failed,
                InteractionStatus.Failed,
                result.ReasonCode,
                ProviderCommandType.Reject,
                now,
                cancellationToken);

            return result;
        }

        result.QueueId = queue.ItemId;

        var priority = plan is not null ? plan.Priority : (InteractionPriority?)null;

        await _queueService.EnqueueAsync(activity.ItemId, queue.ItemId, priority, cancellationToken);
        result.Queued = true;

        var agentUserId = await _offerService.OfferNextAsync(queue.ItemId, cancellationToken);

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

    private async Task TerminalizeInboundAsync(
        OmnichannelActivity activity,
        Core.Models.Interaction interaction,
        ActivityStatus activityStatus,
        InteractionStatus interactionStatus,
        string reasonCode,
        ProviderCommandType providerCommandType,
        DateTime endedUtc,
        CancellationToken cancellationToken)
    {
        ProviderCommandRegistration providerCommand = null;

        if (!string.IsNullOrWhiteSpace(interaction.ProviderName) &&
            !string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
        {
            var commandId = IdGenerator.GenerateId();
            interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId] = commandId;
            providerCommand = new ProviderCommandRegistration
            {
                CommandId = commandId,
                ProviderName = interaction.ProviderName,
                CommandType = providerCommandType,
                ActivityItemId = activity.ItemId,
                InteractionId = interaction.ItemId,
                RemoveReservationFromQueueOnFailure = false,
                RequestPayload = JsonSerializer.Serialize(new ProviderCallActionCommandRequest
                {
                    Initiator = CallControlInitiator.System,
                    ActivityItemId = activity.ItemId,
                    InteractionId = interaction.ItemId,
                    ProviderCallId = interaction.ProviderInteractionId,
                    ReofferOnFailure = false,
                    Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["reasonCode"] = reasonCode,
                    },
                }),
            };
        }

        await _workStateService.MutateAsync(
            activity.ItemId,
            workState => workState.TransitionTo(ActivityAssignmentStatus.Released),
            cancellationToken);

        if (providerCommand is null)
        {
            interaction.TransitionTo(interactionStatus);
        }
        else
        {
            interaction.Reoffer();
        }

        interaction.EndedUtc = providerCommand is null
            ? endedUtc
            : null;
        interaction.TechnicalMetadata[RoutingTerminalReasonMetadataKey] = reasonCode;

        await _activityWriter.ScheduleUpdateAsync(activity.ItemId, terminated =>
        {
            terminated.Status = activityStatus;
            terminated.TerminalReasonCode = reasonCode;
            terminated.CompletedUtc = endedUtc;
        }, cancellationToken);

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        if (providerCommand is not null)
        {
            await _providerCommandStateService.RegisterAsync(providerCommand, cancellationToken);
            _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
                processor.DispatchAsync(providerCommand.CommandId, CancellationToken.None));
        }
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

    private async Task<IReadOnlyList<string>> ResolveContactsAsync(
        string fromAddress,
        CancellationToken cancellationToken)
    {
        var contactIds = await _contactLookup.FindContactItemIdsAsync(fromAddress, cancellationToken);

        return contactIds
            .Where(contactId => !string.IsNullOrEmpty(contactId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<OmnichannelActivity> CreateActivityAsync(
        OmnichannelChannelEndpoint endpoint,
        SubjectFlowSettings flow,
        string fromAddress,
        IReadOnlyList<string> contactItemIds,
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
        activity.Status = ActivityStatus.AwaitingAgentResponse;
        activity.ScheduledUtc = now;
        activity.CreatedUtc = now;
        activity.ContactResolutionCandidates = contactItemIds.ToList();
        activity.ContactResolutionStatus = contactItemIds.Count switch
        {
            0 => ContactResolutionStatus.Unresolved,
            1 => ContactResolutionStatus.Resolved,
            _ => ContactResolutionStatus.Ambiguous,
        };

        if (activity.ContactResolutionStatus == ContactResolutionStatus.Resolved)
        {
            var contact = await _contentManager.GetAsync(contactItemIds[0]);

            if (contact is not null)
            {
                activity.ContactContentItemId = contact.ContentItemId;
                activity.ContactContentType = contact.ContentType;
                activity.ContactResolvedUtc = now;
            }
            else
            {
                activity.ContactResolutionStatus = ContactResolutionStatus.Unresolved;
            }
        }

        if (!string.IsNullOrEmpty(activity.SubjectContentType))
        {
            activity.Subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        // Routing state is created before the activity is persisted so the activity's read model is already
        // reconciled on its first write, instead of costing a second write to converge.
        var workState = await _workStateService.MutateAsync(
            activity.ItemId,
            state => state.TransitionTo(ActivityAssignmentStatus.Available));

        if (workState is not null)
        {
            ContactCenterWorkStateProjector.Apply(activity, workState);
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
