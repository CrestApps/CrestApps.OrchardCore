using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingOutbox"/>. It re-attempts queued outbound messages whose retry time has
/// arrived, respecting a per-endpoint send budget so a backlog of retries cannot burst past what a carrier
/// permits one number to send, and marks a message failed only once its backoff schedule is exhausted.
/// </summary>
public sealed class MessagingOutbox : IMessagingOutbox
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IMessagingRealTimeNotifier _notifier;
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingAttachmentUrlProvider _attachmentUrlProvider;
    private readonly MessagingWorkspaceOptions _options;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingOutbox"/> class.
    /// </summary>
    /// <param name="channelResolver">The resolver of the enabled channels each message is sent through.</param>
    /// <param name="notifier">The real-time notifier that moves the bubble's state in the open thread.</param>
    /// <param name="conversationStore">The conversation store used to address the notification.</param>
    /// <param name="attachmentUrlProvider">The provider of the links a picture message is re-sent with.</param>
    /// <param name="options">The workspace options carrying the batch size and per-endpoint budget.</param>
    /// <param name="session">The session the messages are read from and saved to.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public MessagingOutbox(
        IMessagingChannelResolver channelResolver,
        IMessagingRealTimeNotifier notifier,
        IMessagingConversationStore conversationStore,
        IMessagingAttachmentUrlProvider attachmentUrlProvider,
        IOptions<MessagingWorkspaceOptions> options,
        ISession session,
        IClock clock,
        ILogger<MessagingOutbox> logger)
    {
        _channelResolver = channelResolver;
        _notifier = notifier;
        _conversationStore = conversationStore;
        _attachmentUrlProvider = attachmentUrlProvider;
        _options = options.Value;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        var queued = MessageDeliveryStatus.Queued.ToString();
        var now = _clock.UtcNow;

        var channelNames = _channelResolver.GetAll().Select(channel => channel.Name).ToArray();

        if (channelNames.Length == 0)
        {
            return 0;
        }

        var candidates = await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.Channel.IsIn(channelNames) && !index.IsInbound,
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .Take(Math.Max(1, _options.OutboxBatchSize))
            .ListAsync(cancellationToken);

        var due = candidates
            .Where(message => string.Equals(message.DeliveryStatus, queued, StringComparison.Ordinal))
            .Select(message => (Message: message, State: message.TryGet<OutboundDeliveryState>(out var state) ? state : null))
            .Where(entry => entry.State?.NextAttemptUtc is not null && entry.State.NextAttemptUtc <= now)
            .ToArray();

        if (due.Length == 0)
        {
            return 0;
        }

        // A per-endpoint budget applied across the pass, so a backlog on one endpoint cannot burst past the rate
        // the provider permits and get that endpoint blocked.
        var perEndpointBudget = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var maxPerEndpoint = Math.Max(1, _options.MaxMessagesPerPassPerEndpoint);
        var accepted = 0;
        var attempted = 0;

        foreach (var (message, state) in due)
        {
            var channel = _channelResolver.Get(message.Channel);

            if (channel is null)
            {
                continue;
            }

            var endpoint = $"{message.Channel}:{message.ServiceAddress}";

            perEndpointBudget.TryGetValue(endpoint, out var used);

            if (used >= maxPerEndpoint)
            {
                continue;
            }

            perEndpointBudget[endpoint] = used + 1;
            attempted++;

            // A picture message is re-sent with fresh links: the ones the first attempt carried may have expired.
            var mediaUrls = await BuildMediaUrlsAsync(channel, message, cancellationToken);

            // The retry carries everything the first attempt did, the subject and the thread included, so the
            // contact receives the same message rather than a stripped-down copy of it.
            var dispatch = mediaUrls is null
                ? MessageDispatchResult.Failed("The site has no public address the provider could download the pictures from. Set the site's base URL.")
                : await channel.SendAsync(
                    new MessagingOutboundMessage
                    {
                        ServiceAddress = message.ServiceAddress,
                        ContactAddress = message.CustomerAddress,
                        ConversationId = message.ConversationId,
                        Purpose = state.Purpose,
                        Subject = message.GetSubject(),
                        Body = message.Content,
                        MediaUrls = mediaUrls,
                        Attachments = message.GetAttachments().ToList(),
                    },
                    cancellationToken);

            state.Attempts += 1;

            if (dispatch.Succeeded)
            {
                state.NextAttemptUtc = null;
                state.LastError = null;

                message.DeliveryStatus = MessageDeliveryStatus.Sent.ToString();
                message.ProviderMessageId = dispatch.ProviderMessageId;
                message.ErrorCode = null;
                accepted++;
            }
            else
            {
                state.LastError = dispatch.GetErrorText();

                if (OutboundDeliveryState.CanRetry(state.Attempts, dispatch.ErrorCode))
                {
                    state.NextAttemptUtc = now.Add(OutboundDeliveryState.GetDelay(state.Attempts));
                }
                else
                {
                    state.NextAttemptUtc = null;
                    message.DeliveryStatus = MessageDeliveryStatus.Failed.ToString();
                }

                message.ErrorCode = state.LastError;
            }

            message.Put(state);

            await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

            await NotifyAsync(message, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The messaging outbox re-attempted {Attempted} queued messages and {Accepted} were accepted.",
                attempted,
                accepted);
        }

        return accepted;
    }

    private async Task<IList<string>> BuildMediaUrlsAsync(IMessagingChannel channel, OmnichannelMessage message, CancellationToken cancellationToken)
    {
        var urls = message.MediaReferences?.Where(url => !string.IsNullOrWhiteSpace(url)).ToList() ?? [];

        if (!channel.Capabilities.Attachments.DeliveredAsLinks)
        {
            return urls;
        }

        foreach (var attachment in message.GetAttachments())
        {
            var url = await _attachmentUrlProvider.GetPublicUrlAsync(attachment, cancellationToken);

            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            urls.Add(url);
        }

        return urls;
    }

    private async Task NotifyAsync(OmnichannelMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message.ConversationId))
        {
            return;
        }

        var conversation = await _conversationStore.FindByIdAsync(message.ConversationId, cancellationToken);

        if (conversation is null)
        {
            return;
        }

        await _notifier.MessageDeliveryUpdatedAsync(
            new MessagingDeliveryNotification
            {
                ConversationId = conversation.ItemId,
                MessageId = message.Id,
                Status = Enum.TryParse<MessageDeliveryStatus>(message.DeliveryStatus, out var status) ? status : MessageDeliveryStatus.Queued,
                ErrorCode = message.ErrorCode,
                AssignedAgentId = conversation.AssignedAgentId,
                OwnerQueueId = conversation.OwnerType == ConversationOwnerType.Queue ? conversation.OwnerId : null,
            },
            cancellationToken);
    }
}
