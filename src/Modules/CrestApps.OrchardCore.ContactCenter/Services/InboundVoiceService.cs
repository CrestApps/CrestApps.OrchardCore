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
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IInboundVoiceService"/> implementation. It creates the CRM activity and the
/// interaction for an inbound call, resolves the target queue and subject, enqueues the work, reserves
/// an available agent, and offers the ringing call to that agent through the Telephony soft phone.
/// </summary>
public sealed class InboundVoiceService : IInboundVoiceService
{
    private const string ServiceAddressMetadataKey = "serviceAddress";

    private readonly IOmnichannelChannelEndpointManager _channelEndpointManager;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContentManager _contentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IActivityQueueService _queueService;
    private readonly IActivityAssignmentService _assignmentService;
    private readonly IAgentProfileManager _agentManager;
    private readonly IInboundContactLookup _contactLookup;
    private readonly IIncomingCallDispatcher _incomingCallDispatcher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundVoiceService"/> class.
    /// </summary>
    /// <param name="channelEndpointManager">The channel endpoint manager used to map the dialed number to an endpoint.</param>
    /// <param name="subjectFlowSettingsService">The subject flow settings service used to resolve the subject and campaign.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="contentManager">The content manager used to create the subject and load contacts.</param>
    /// <param name="interactionManager">The interaction manager used to record communication history.</param>
    /// <param name="queueManager">The queue manager used to resolve the inbound queue.</param>
    /// <param name="queueService">The queue service used to enqueue the activity.</param>
    /// <param name="assignmentService">The assignment service used to reserve an available agent.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the reserved agent.</param>
    /// <param name="contactLookup">The contact lookup used to resolve the caller.</param>
    /// <param name="incomingCallDispatcher">The dispatcher used to offer the ringing call to the agent.</param>
    /// <param name="clock">The clock used to stamp times.</param>
    public InboundVoiceService(
        IOmnichannelChannelEndpointManager channelEndpointManager,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IOmnichannelActivityManager activityManager,
        IContentManager contentManager,
        IInteractionManager interactionManager,
        IActivityQueueManager queueManager,
        IActivityQueueService queueService,
        IActivityAssignmentService assignmentService,
        IAgentProfileManager agentManager,
        IInboundContactLookup contactLookup,
        IIncomingCallDispatcher incomingCallDispatcher,
        IClock clock)
    {
        _channelEndpointManager = channelEndpointManager;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _activityManager = activityManager;
        _contentManager = contentManager;
        _interactionManager = interactionManager;
        _queueManager = queueManager;
        _queueService = queueService;
        _assignmentService = assignmentService;
        _agentManager = agentManager;
        _contactLookup = contactLookup;
        _incomingCallDispatcher = incomingCallDispatcher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<InboundVoiceRoutingResult> HandleInboundAsync(InboundVoiceEvent inboundEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);

        var result = new InboundVoiceRoutingResult();
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

        var queue = await ResolveQueueAsync(endpoint, cancellationToken);

        var interaction = await CreateInteractionAsync(inboundEvent, activity, queue, fromAddress, serviceAddress);
        result.InteractionId = interaction.ItemId;

        if (queue is null)
        {
            result.Reason = "No inbound queue is configured to receive this call.";

            return result;
        }

        result.QueueId = queue.ItemId;

        await _queueService.EnqueueAsync(activity.ItemId, queue.ItemId, priority: null, cancellationToken);

        var agentUserId = await OfferNextAsync(queue.ItemId, cancellationToken);

        if (string.IsNullOrEmpty(agentUserId))
        {
            result.Reason = "The call was queued; no agent is currently available.";

            return result;
        }

        result.Routed = true;
        result.AgentUserId = agentUserId;
        result.Reason = "Offered to an available agent.";

        return result;
    }

    /// <inheritdoc/>
    public async Task<string> OfferNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var reservation = await _assignmentService.AssignNextAsync(queueId, cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        if (agent is null || string.IsNullOrEmpty(agent.UserId))
        {
            return null;
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

        if (interaction is null)
        {
            return null;
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
        };

        await _incomingCallDispatcher.DispatchAsync(agent.UserId, call, cancellationToken);

        return agent.UserId;
    }

    private static string ResolveServiceAddress(Core.Models.Interaction interaction)
    {
        return interaction.TechnicalMetadata.TryGetValue(ServiceAddressMetadataKey, out var value)
            ? value?.ToString()
            : null;
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
