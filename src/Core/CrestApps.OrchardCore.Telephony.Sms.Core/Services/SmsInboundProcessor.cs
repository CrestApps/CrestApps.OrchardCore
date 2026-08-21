using CrestApps.Core;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Models;
using CrestApps.OrchardCore.Telephony.Sms.Notifications;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The human inbound pipeline for the SMS portal. Listening on the shared <see cref="IOmnichannelEventHandler"/>
/// bus, it turns a received SMS into (or appends it to) a human <see cref="SmsConversation"/> and routes the
/// conversation to an owner — so no inbound is silently dropped. It yields to the existing automated (AI) path
/// while an automated activity is still handling the number, and takes over after a handoff.
/// </summary>
public sealed class SmsInboundProcessor : IOmnichannelEventHandler, ISmsInboundProcessor
{
    private const int PreviewLength = 120;

    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsContactResolver _contactResolver;
    private readonly ISmsRealTimeNotifier _notifier;
    private readonly IEnumerable<ISmsInboundRouter> _routers;
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly Redactor _addressRedactor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsInboundProcessor"/> class.
    /// </summary>
    public SmsInboundProcessor(
        IOmnichannelChannelEndpointManager endpointManager,
        IOmnichannelActivityStore activityStore,
        ISmsConversationStore conversationStore,
        ISmsContactResolver contactResolver,
        ISmsRealTimeNotifier notifier,
        IEnumerable<ISmsInboundRouter> routers,
        IContentManager contentManager,
        ISession session,
        IClock clock,
        IRedactorProvider redactorProvider,
        ILogger<SmsInboundProcessor> logger)
    {
        _endpointManager = endpointManager;
        _activityStore = activityStore;
        _conversationStore = conversationStore;
        _contactResolver = contactResolver;
        _notifier = notifier;
        _routers = routers;
        _contentManager = contentManager;
        _session = session;
        _clock = clock;
        _addressRedactor = redactorProvider.GetRedactor(LogDataClassifications.AddressSet);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        if (omnichannelEvent?.Message is null ||
            omnichannelEvent.EventType != OmnichannelConstants.Events.SmsReceived ||
            omnichannelEvent.Message.Channel != OmnichannelConstants.Channels.Sms ||
            !omnichannelEvent.Message.IsInbound)
        {
            return;
        }

        await ProcessAsync(omnichannelEvent.Message, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SmsConversation> ProcessAsync(OmnichannelMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var serviceAddress = message.ServiceAddress.GetCleanedPhoneNumber();
        var customerAddress = message.CustomerAddress.GetCleanedPhoneNumber();

        var endpoint = await _endpointManager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Sms, serviceAddress, cancellationToken);

        if (endpoint is null)
        {
            _logger.LogWarning("No channel endpoint found for incoming SMS message. Service Address: {ServiceAddress}", _addressRedactor.Redact(message.ServiceAddress));

            return null;
        }

        var conversation = await _conversationStore.FindByAddressesAsync(serviceAddress, customerAddress, cancellationToken);

        var isNew = conversation is null;

        // Yield to the automated (AI) path while it still owns the number and no human thread exists yet. After
        // an AI-to-human handoff the human SmsConversation already exists, so this guard no longer trips and the
        // existing-conversation router keeps replies in the human thread.
        if (isNew)
        {
            var automatedActivity = await _activityStore.GetAsync(
                OmnichannelConstants.Channels.Sms,
                endpoint.ItemId,
                message.CustomerAddress,
                ActivityInteractionType.Automated,
                cancellationToken);

            if (automatedActivity is not null &&
                automatedActivity.Status is not (ActivityStatus.Completed or ActivityStatus.Cancelled))
            {
                return null;
            }

            conversation = new SmsConversation
            {
                ItemId = UniqueId.GenerateId(),
                Channel = OmnichannelConstants.Channels.Sms,
                ServiceAddress = serviceAddress,
                CustomerAddress = customerAddress,
                Status = SmsConversationStatus.Open,
                AssignmentStatus = SmsConversationAssignmentStatus.Unassigned,
                CreatedUtc = _clock.UtcNow,
                ContactContentItemId = await _contactResolver.ResolveContactContentItemIdAsync(customerAddress, cancellationToken),
            };
        }

        var context = new SmsInboundRoutingContext
        {
            Message = message,
            Endpoint = endpoint,
            Conversation = conversation,
            IsNewConversation = isNew,
        };

        foreach (var router in _routers.OrderBy(r => r.Order))
        {
            if (await router.TryRouteAsync(context, cancellationToken))
            {
                break;
            }
        }

        // Honor STOP/opt-out keywords: flag the contact Do-not-SMS and auto-close the thread, but still persist
        // the message so the opt-out is visible in the transcript.
        if (OmnichannelSmsComplianceHelper.IsOptOutRequest(message.Content))
        {
            await ApplyOptOutAsync(conversation, cancellationToken);
        }

        // Roll up the thread and link the message to it.
        conversation.LastMessageUtc = message.CreatedUtc == default ? _clock.UtcNow : message.CreatedUtc;
        conversation.LastMessagePreview = BuildPreview(message.Content);
        conversation.UnreadCount += 1;
        conversation.IsRead = false;
        conversation.ModifiedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _conversationStore.CreateAsync(conversation, cancellationToken);
        }
        else
        {
            await _conversationStore.UpdateAsync(conversation, cancellationToken);
        }

        message.ConversationId = conversation.ItemId;

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        await _notifier.NewInboundMessageAsync(new SmsInboundNotification
        {
            ConversationId = conversation.ItemId,
            ServiceAddress = conversation.ServiceAddress,
            CustomerAddress = conversation.CustomerAddress,
            Preview = conversation.LastMessagePreview,
            UnreadCount = conversation.UnreadCount,
            ReceivedUtc = conversation.LastMessageUtc ?? _clock.UtcNow,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = conversation.OwnerType == SmsConversationOwnerType.Queue ? conversation.OwnerId : null,
        }, cancellationToken);

        return conversation;
    }

    private async Task ApplyOptOutAsync(SmsConversation conversation, CancellationToken cancellationToken)
    {
        conversation.Status = SmsConversationStatus.Closed;

        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        if (contact is null)
        {
            return;
        }

        contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(true, _clock.UtcNow));

        await _contentManager.UpdateAsync(contact);
    }

    private static string BuildPreview(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var normalized = content.ReplaceLineEndings(" ").Trim();

        return normalized.Length <= PreviewLength
            ? normalized
            : normalized[..PreviewLength];
    }
}
