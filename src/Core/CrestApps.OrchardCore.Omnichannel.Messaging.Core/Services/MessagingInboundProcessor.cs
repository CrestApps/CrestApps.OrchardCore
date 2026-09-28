using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The human inbound pipeline of the messaging workspace, shared by every channel. A channel's receiver hands it a
/// normalized inbound message; it turns that message into (or appends it to) a <see cref="MessagingConversation"/>
/// and routes the conversation to an owner, so no inbound is silently dropped. It yields to the automated (AI)
/// path while an automated activity is still handling the contact on that endpoint, and takes over after a
/// handoff. What is particular to a channel — the carrier keywords of a text message, for instance — is applied by
/// that channel's <see cref="IMessagingInboundHandler"/>.
/// </summary>
public sealed class MessagingInboundProcessor : IMessagingInboundProcessor
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingContactResolver _contactResolver;
    private readonly IMessagingRealTimeNotifier _notifier;
    private readonly IMessagingConversationRouter _router;
    private readonly IMessagingFirstResponseSlaService _slaService;
    private readonly IEnumerable<IMessagingInboundHandler> _inboundHandlers;
    private readonly IDistributedLock _distributedLock;
    private readonly MessagingWorkspaceOptions _options;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly Redactor _addressRedactor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingInboundProcessor"/> class.
    /// </summary>
    public MessagingInboundProcessor(
        IMessagingChannelResolver channelResolver,
        IOmnichannelChannelEndpointManager endpointManager,
        IOmnichannelActivityStore activityStore,
        IMessagingConversationStore conversationStore,
        IMessagingContactResolver contactResolver,
        IMessagingRealTimeNotifier notifier,
        IMessagingConversationRouter router,
        IMessagingFirstResponseSlaService slaService,
        IEnumerable<IMessagingInboundHandler> inboundHandlers,
        IDistributedLock distributedLock,
        IOptions<MessagingWorkspaceOptions> options,
        ISession session,
        IClock clock,
        IRedactorProvider redactorProvider,
        ILogger<MessagingInboundProcessor> logger)
    {
        _channelResolver = channelResolver;
        _endpointManager = endpointManager;
        _activityStore = activityStore;
        _conversationStore = conversationStore;
        _contactResolver = contactResolver;
        _notifier = notifier;
        _router = router;
        _slaService = slaService;
        _inboundHandlers = inboundHandlers.OrderBy(handler => handler.Order).ToArray();
        _distributedLock = distributedLock;
        _options = options.Value;
        _session = session;
        _clock = clock;
        _addressRedactor = redactorProvider.GetRedactor(LogDataClassifications.AddressSet);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MessagingConversation> ProcessAsync(OmnichannelMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var channel = _channelResolver.Get(message.Channel);

        if (channel is null)
        {
            _logger.LogWarning("An inbound message arrived on channel '{Channel}', which no enabled messaging channel serves.", message.Channel?.SanitizeLogValue());

            return null;
        }

        var serviceAddress = channel.NormalizeAddress(message.ServiceAddress);
        var contactAddress = channel.NormalizeAddress(message.CustomerAddress);

        if (string.IsNullOrEmpty(serviceAddress) || string.IsNullOrEmpty(contactAddress))
        {
            _logger.LogWarning("An inbound {Channel} message arrived without both a service and a contact address.", channel.Name);

            return null;
        }

        var endpoint = await _endpointManager.GetByServiceAddressAsync(channel.Name, serviceAddress, cancellationToken);

        if (endpoint is null)
        {
            _logger.LogWarning("No channel endpoint found for an incoming {Channel} message. Service Address: {ServiceAddress}", channel.Name, _addressRedactor.Redact(message.ServiceAddress));

            return null;
        }

        // Find-or-create and the thread roll-up are serialized per address pair. Without this, two messages arriving
        // in the same second from a new contact each see "no conversation" and each create one.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            $"MessagingConversation:{channel.Name}:{serviceAddress}:{contactAddress}",
            _options.ConversationLockTimeout,
            _options.ConversationLockExpiration);

        if (!locked)
        {
            // Dropping the message would lose it silently. Failing lets the durable provider inbox redeliver it.
            throw new InvalidOperationException(
                "The inbound message could not acquire its conversation lock. The delivery will be retried.");
        }

        await using (locker)
        {
            return await ProcessLockedAsync(channel, message, endpoint, serviceAddress, contactAddress, cancellationToken);
        }
    }

    private async Task<MessagingConversation> ProcessLockedAsync(
        IMessagingChannel channel,
        OmnichannelMessage message,
        OmnichannelChannelEndpoint endpoint,
        string serviceAddress,
        string contactAddress,
        CancellationToken cancellationToken)
    {
        // Yield to the automated (AI) path while a live automated activity owns the contact on this endpoint — whether
        // or not a human thread already exists for it. The automated handler answers the message, and a handoff copies
        // the whole automated transcript into the human thread, so that copy is the one writer for these messages.
        // Recording them here as well put every message the AI handled into the thread twice, and routed, notified
        // and started the first-response clock on a thread the AI was still answering. Once the activity concludes
        // (the handoff concludes it), messages flow into the human thread from here again.
        var automatedActivity = await _activityStore.GetAsync(
            channel.Name,
            endpoint.ItemId,
            message.CustomerAddress,
            ActivityInteractionType.Automated,
            cancellationToken);

        if (automatedActivity is not null && !automatedActivity.Status.IsTerminal())
        {
            return null;
        }

        var conversation = await _conversationStore.FindByAddressesAsync(channel.Name, serviceAddress, contactAddress, cancellationToken);

        // The provider delivers at least once, and the message that asked for a person reaches here after the handoff
        // already copied it across. Either way the thread holds this message already: it is not a new inbound, so it
        // is neither recorded, routed nor announced again.
        if (conversation is not null &&
            await _conversationStore.ContainsMessageAsync(conversation.ItemId, message.ProviderMessageId, cancellationToken))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The inbound {Channel} message {ProviderMessageId} is already in conversation {ConversationId}; it is not recorded again.",
                    channel.Name,
                    message.ProviderMessageId.SanitizeLogValue(),
                    conversation.ItemId.SanitizeLogValue());
            }

            return conversation;
        }

        var isNew = conversation is null;

        if (isNew)
        {
            conversation = new MessagingConversation
            {
                ItemId = UniqueId.GenerateId(),
                Channel = channel.Name,
                ServiceAddress = serviceAddress,
                ContactAddress = contactAddress,
                Status = ConversationStatus.Open,
                AssignmentStatus = ConversationAssignmentStatus.Unassigned,
                CreatedUtc = _clock.UtcNow,
                ContactContentItemId = await _contactResolver.ResolveContactContentItemIdAsync(channel.Name, contactAddress, cancellationToken),
            };
        }

        var inboundContext = new MessagingInboundContext
        {
            Channel = channel,
            Message = message,
            Endpoint = endpoint,
            Conversation = conversation,
            IsNewConversation = isNew,
        };

        foreach (var handler in _inboundHandlers)
        {
            await handler.ReceivingAsync(inboundContext, cancellationToken);
        }

        await _router.RouteAsync(
            new MessagingRoutingContext
            {
                Trigger = MessagingRoutingTrigger.Inbound,
                Channel = channel,
                Message = message,
                Endpoint = endpoint,
                Conversation = conversation,
                IsNewConversation = isNew,
                SuppressAutomatedReplies = inboundContext.SuppressAutomatedReplies,
            },
            cancellationToken);

        // The customer is now waiting on a person, so the first-response clock starts. It is a no-op on a queue
        // with no target, and on a thread that already has a deadline running.
        await _slaService.ApplyFirstResponseTargetAsync(conversation, cancellationToken);

        // The channel's own rules. The inbound message is still persisted whatever they decide, so the transcript
        // shows what the contact actually sent.
        foreach (var handler in _inboundHandlers)
        {
            await handler.ReceivedAsync(inboundContext, cancellationToken);
        }

        // Roll up the thread and link the message to it.
        MessagingConversationRollup.ApplyInbound(conversation, message.Content, message.CreatedUtc, _clock.UtcNow);

        if (isNew)
        {
            conversation = await CreateOrAdoptAsync(conversation, channel.Name, serviceAddress, contactAddress, cancellationToken);
        }
        else
        {
            await _conversationStore.UpdateAsync(conversation, cancellationToken);
        }

        message.ConversationId = conversation.ItemId;

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        await _notifier.NewInboundMessageAsync(new MessagingInboundNotification
        {
            ConversationId = conversation.ItemId,
            Channel = conversation.Channel,
            CustomerKey = conversation.GetCustomerKey(),
            ServiceAddress = conversation.ServiceAddress,
            ContactAddress = conversation.ContactAddress,
            Preview = conversation.LastMessagePreview,
            UnreadCount = conversation.UnreadCount,
            ReceivedUtc = conversation.LastMessageUtc ?? _clock.UtcNow,
            AssignedAgentId = conversation.AssignedAgentId,
            OwnerQueueId = conversation.OwnerType == ConversationOwnerType.Queue ? conversation.OwnerId : null,
        }, cancellationToken);

        return conversation;
    }

    // The unique index on (Channel, ServiceAddress, ContactAddress) is the last line of defence when two nodes get
    // past the lock - a lock expiry, a Redis failover, or a tenant with no distributed lock provider. Losing that
    // race must land the message on the thread the winner created rather than fail the delivery, so the create is
    // followed by a re-read when the database refuses the duplicate.
    private async Task<MessagingConversation> CreateOrAdoptAsync(
        MessagingConversation conversation,
        string channel,
        string serviceAddress,
        string contactAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            await _conversationStore.CreateAsync(conversation, cancellationToken);

            return conversation;
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            var existing = await _conversationStore.FindByAddressesAsync(channel, serviceAddress, contactAddress, cancellationToken);

            if (existing is null)
            {
                throw;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Another node created the conversation for this address pair first; the inbound message was appended to conversation {ConversationId}.",
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
            if (current.Message.Contains(MessagingStorage.ConversationAddressesUniqueIndexName, StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
