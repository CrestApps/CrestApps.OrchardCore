using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The human inbound pipeline for the SMS portal. Listening on the shared <see cref="IOmnichannelEventHandler"/>
/// bus, it turns a received SMS into (or appends it to) a human <see cref="SmsConversation"/> and routes the
/// conversation to an owner — so no inbound is silently dropped. It yields to the existing automated (AI) path
/// while an automated activity is still handling the number, and takes over after a handoff.
/// </summary>
public sealed class SmsInboundProcessor : IOmnichannelEventHandler, ISmsInboundProcessor
{

    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsContactResolver _contactResolver;
    private readonly ISmsRealTimeNotifier _notifier;
    private readonly ISmsConversationRouter _router;
    private readonly ISmsFirstResponseSlaService _slaService;
    private readonly ISmsDispatcher _dispatcher;
    private readonly SmsKeywordReplySettings _keywordReplySettings;
    private readonly IContentManager _contentManager;
    private readonly IDistributedLock _distributedLock;
    private readonly SmsPortalOptions _options;
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
        ISmsConversationRouter router,
        ISmsFirstResponseSlaService slaService,
        ISmsDispatcher dispatcher,
        IOptions<SmsKeywordReplySettings> keywordReplySettings,
        IContentManager contentManager,
        IDistributedLock distributedLock,
        IOptions<SmsPortalOptions> options,
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
        _router = router;
        _slaService = slaService;
        _dispatcher = dispatcher;
        _keywordReplySettings = keywordReplySettings.Value;
        _contentManager = contentManager;
        _distributedLock = distributedLock;
        _options = options.Value;
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
        var contactAddress = message.CustomerAddress.GetCleanedPhoneNumber();

        var endpoint = await _endpointManager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Sms, serviceAddress, cancellationToken);

        if (endpoint is null)
        {
            _logger.LogWarning("No channel endpoint found for incoming SMS message. Service Address: {ServiceAddress}", _addressRedactor.Redact(message.ServiceAddress));

            return null;
        }

        // Find-or-create and the thread roll-up are serialized per number pair. Without this, two texts arriving
        // in the same second from a new number each see "no conversation" and each create one.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            $"SmsConversation:{serviceAddress}:{contactAddress}",
            _options.ConversationLockTimeout,
            _options.ConversationLockExpiration);

        if (!locked)
        {
            // Dropping the message would lose it silently. Failing lets the durable provider inbox redeliver it.
            throw new InvalidOperationException(
                "The inbound SMS could not acquire its conversation lock. The delivery will be retried.");
        }

        await using (locker)
        {
            return await ProcessLockedAsync(message, endpoint, serviceAddress, contactAddress, cancellationToken);
        }
    }

    private async Task<SmsConversation> ProcessLockedAsync(
        OmnichannelMessage message,
        OmnichannelChannelEndpoint endpoint,
        string serviceAddress,
        string contactAddress,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversationStore.FindByAddressesAsync(serviceAddress, contactAddress, cancellationToken);

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

            if (automatedActivity is not null && !automatedActivity.Status.IsTerminal())
            {
                return null;
            }

            conversation = new SmsConversation
            {
                ItemId = UniqueId.GenerateId(),
                Channel = OmnichannelConstants.Channels.Sms,
                ServiceAddress = serviceAddress,
                ContactAddress = contactAddress,
                Status = SmsConversationStatus.Open,
                AssignmentStatus = SmsConversationAssignmentStatus.Unassigned,
                CreatedUtc = _clock.UtcNow,
                ContactContentItemId = await _contactResolver.ResolveContactContentItemIdAsync(contactAddress, cancellationToken),
            };
        }

        await _router.RouteAsync(
            new SmsRoutingContext
            {
                Trigger = SmsRoutingTrigger.Inbound,
                Message = message,
                Endpoint = endpoint,
                Conversation = conversation,
                IsNewConversation = isNew,
            },
            cancellationToken);

        // The customer is now waiting on a person, so the first-response clock starts. It is a no-op on a queue
        // with no target, and on a thread that already has a deadline running.
        await _slaService.ApplyFirstResponseTargetAsync(conversation, cancellationToken);

        // The carrier keywords. STOP used to close the thread silently, which is not compliant: the contact is
        // owed a confirmation. HELP and START were not recognised at all, so a contact who had opted out had no
        // supported way back in. The inbound message is still persisted either way, so the transcript shows what
        // the contact actually sent.
        await ApplyKeywordAsync(conversation, message, cancellationToken);

        // Roll up the thread and link the message to it.
        SmsConversationRollup.ApplyInbound(conversation, message.Content, message.CreatedUtc, _clock.UtcNow);

        if (isNew)
        {
            conversation = await CreateOrAdoptAsync(conversation, serviceAddress, contactAddress, message, cancellationToken);
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
            ContactAddress = conversation.ContactAddress,
            Preview = conversation.LastMessagePreview,
            UnreadCount = conversation.UnreadCount,
            ReceivedUtc = conversation.LastMessageUtc ?? _clock.UtcNow,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = conversation.OwnerType == SmsConversationOwnerType.Queue ? conversation.OwnerId : null,
        }, cancellationToken);

        return conversation;
    }

    // The unique index on (ServiceAddress, ContactAddress) is the last line of defence when two nodes get past
    // the lock - a lock expiry, a Redis failover, or a tenant with no distributed lock provider. Losing that race
    // must land the message on the thread the winner created rather than fail the delivery, so the create is
    // followed by a re-read when the database refuses the duplicate.
    private async Task<SmsConversation> CreateOrAdoptAsync(
        SmsConversation conversation,
        string serviceAddress,
        string contactAddress,
        OmnichannelMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await _conversationStore.CreateAsync(conversation, cancellationToken);

            return conversation;
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            var existing = await _conversationStore.FindByAddressesAsync(serviceAddress, contactAddress, cancellationToken);

            if (existing is null)
            {
                throw;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Another node created the SMS conversation for this number pair first; the inbound message was appended to conversation {ConversationId}.",
                    existing.ItemId.SanitizeLogValue());
            }

            existing.LastMessageUtc = conversation.LastMessageUtc;
            existing.LastMessagePreview = conversation.LastMessagePreview;
            existing.UnreadCount += 1;
            existing.IsRead = false;
            existing.ModifiedUtc = conversation.ModifiedUtc;

            await _conversationStore.UpdateAsync(existing, cancellationToken);

            return existing;
        }
    }

    // YesSql surfaces a duplicate-key refusal as whatever the database driver threw, so the unique index name is
    // what identifies it across providers.
    private static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(SmsPortalStorage.ConversationAddressesUniqueIndexName, StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task ApplyKeywordAsync(SmsConversation conversation, OmnichannelMessage message, CancellationToken cancellationToken)
    {
        var keyword = SmsKeywordPolicy.Classify(message.Content);

        if (keyword == SmsKeyword.None)
        {
            return;
        }

        switch (keyword)
        {
            case SmsKeyword.Stop:
                conversation.Status = SmsConversationStatus.Closed;
                await SetDoNotSmsAsync(conversation, true, cancellationToken);

                break;

            case SmsKeyword.Start:
                // An opt-in keyword only means "make me reachable again" when the contact is actually opted out.
                // "YES" is one of them and is also the most ordinary answer there is — an automated agent opens
                // by asking a yes/no question, so the customer's "Yes." would otherwise be read as a resubscribe:
                // they get a confirmation they never asked for, landing as a second, unrelated message on top of
                // the agent's real reply. Nothing to reopen means nothing to confirm, so leave the message to the
                // conversation. STOP and HELP stay unconditional — those the carrier rules require us to answer
                // however the thread is going.
                if (!await IsOptedOutAsync(conversation, cancellationToken))
                {
                    return;
                }

                // Reopening is the point: a contact who texts START is asking to be reachable again, and leaving
                // the thread closed would mean their next message arrives with no history attached.
                conversation.Status = SmsConversationStatus.Open;
                await SetDoNotSmsAsync(conversation, false, cancellationToken);

                break;
        }

        var reply = SmsKeywordPolicy.ReplyFor(keyword, _keywordReplySettings);

        if (string.IsNullOrWhiteSpace(reply))
        {
            return;
        }

        // Sent through the dispatcher rather than the conversation service, because the conversation service
        // refuses to send to a contact who has opted out — and this confirmation is the one message that must
        // still reach them.
        await _dispatcher.SendAsync(
            new SmsMessage
            {
                To = conversation.ContactAddress,
                From = conversation.ServiceAddress,
                Body = reply,
            },
            cancellationToken);
    }

    /// <summary>
    /// Determines whether the conversation's contact is currently opted out of SMS, which is what makes an
    /// opt-in keyword meaningful. A conversation with no contact record has no recorded opt-out to reverse.
    /// </summary>
    private async Task<bool> IsOptedOutAsync(SmsConversation conversation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return false;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        // TryGet rather than As: a contact that has never carried the part should read as "not opted out", not
        // have an empty one created on it during what is only a question.
        return contact is not null
            && contact.TryGet<OmnichannelContactPart>(out var contactPart)
            && contactPart.DoNotSms;
    }

    private async Task SetDoNotSmsAsync(SmsConversation conversation, bool doNotSms, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(conversation.ContactContentItemId))
        {
            return;
        }

        var contact = await _contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        if (contact is null)
        {
            return;
        }

        contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(doNotSms, _clock.UtcNow));

        await _contentManager.UpdateAsync(contact);
    }

}
