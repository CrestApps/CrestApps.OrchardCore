using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;

/// <summary>
/// Sends the auto-reply of the entry point that answers the endpoint, on whatever channel the conversation runs on,
/// and its closed auto-reply instead while the entry point is closed. The setting was
/// stored by the editor and sent by nothing, so an operator could configure an acknowledgement, see it saved, and
/// watch every contact get silence.
/// <para>
/// It runs first and never claims the conversation: the auto-reply is a side effect, not an ownership decision,
/// and claiming would stop the routers that actually place the thread from running at all.
/// </para>
/// <para>
/// The acknowledgement is kept on the thread as an automatic outbound message, so whoever opens the conversation sees the
/// contact was already answered and does not send a second "we got your message".
/// </para>
/// </summary>
public sealed class AutoReplyRouter : IMessagingInboundRouter
{
    private static readonly TimeSpan _minimumInterval = TimeSpan.FromDays(1);

    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoReplyRouter"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager, used to read the contact's opt-out state.</param>
    /// <param name="session">The session the acknowledgement is kept on the thread through.</param>
    /// <param name="clock">The clock.</param>
    public AutoReplyRouter(IContentManager contentManager, ISession session, IClock clock)
    {
        _contentManager = contentManager;
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public int Order => 50;

    /// <inheritdoc/>
    public async Task<bool> TryRouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Trigger != MessagingRoutingTrigger.Inbound ||
            context.Endpoint is null ||
            context.Message is null ||
            context.Channel is null)
        {
            return false;
        }

        // The channel has already given this message the one answer it is owed (a carrier keyword confirmation, for
        // instance). An auto-reply on top of it is a second message to someone who has just told us to stop.
        if (context.SuppressAutomatedReplies)
        {
            return false;
        }

        var routing = context.Routing;

        if (routing is null || string.IsNullOrWhiteSpace(routing.AutoReplyMessage))
        {
            return false;
        }

        var conversation = context.Conversation;
        var now = _clock.UtcNow;

        // Once a day per thread. A contact who sends three messages in a row should not get three
        // acknowledgements; that is a machine talking over someone trying to reach a person.
        if (conversation.LastAutoReplyUtc is not null && now - conversation.LastAutoReplyUtc < _minimumInterval)
        {
            return false;
        }

        // A contact who has opted out gets nothing automated, the acknowledgement included. The agent-facing
        // send path already refuses them; an automated reply that slipped past it would be the platform
        // messaging someone who asked it not to.
        if (await context.Channel.IsConversationOptedOutAsync(conversation, _contentManager, cancellationToken))
        {
            return false;
        }

        var dispatch = await context.Channel.SendAsync(
            new MessagingOutboundMessage
            {
                ContactAddress = conversation.ContactAddress,
                ServiceAddress = conversation.ServiceAddress,
                ConversationId = context.IsNewConversation ? null : conversation.ItemId,
                ReplyTo = context.Message,
                Purpose = MessagingOutboundPurpose.AutoReply,
                Body = routing.AutoReplyMessage,
            },
            cancellationToken);

        conversation.LastAutoReplyUtc = now;

        // Kept on the thread the way an agent's reply is, but it answers nothing: the contact still waits on a person, so
        // the thread stays unread and its first-response clock keeps running. Saving a message routes nothing, so it
        // cannot set off another auto-reply either.
        await _session.SaveAsync(
            CreateThreadMessage(conversation, context.Message, routing.AutoReplyMessage, dispatch, now),
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: cancellationToken);

        return false;
    }

    // The acknowledgement as the thread shows it: an automatic outbound message no agent wrote, carrying what the
    // provider said about it, so the bubble shows whether it reached the contact and a delivery receipt finds it by its id.
    private static OmnichannelMessage CreateThreadMessage(
        MessagingConversation conversation,
        OmnichannelMessage inbound,
        string body,
        MessageDispatchResult dispatch,
        DateTime utcNow)
    {
        var message = new OmnichannelMessage
        {
            Id = UniqueId.GenerateId(),
            Channel = conversation.Channel,
            CustomerAddress = conversation.ContactAddress,
            ServiceAddress = conversation.ServiceAddress,
            Content = body,

            // It answers the inbound message, so it never sorts above it, whichever clock stamped the inbound message.
            CreatedUtc = inbound.CreatedUtc >= utcNow ? inbound.CreatedUtc.AddTicks(1) : utcNow,
            IsInbound = false,

            // A new conversation already has its identifier; the inbound pipeline stores it in the same unit of work.
            ConversationId = conversation.ItemId,
        };

        message.SetAutomatic(true);

        var state = new OutboundDeliveryState
        {
            Purpose = MessagingOutboundPurpose.AutoReply,
        };

        if (dispatch?.Succeeded == true)
        {
            state.Attempts = 1;

            message.DeliveryStatus = MessageDeliveryStatus.Sent.ToString();
            message.ProviderMessageId = dispatch.ProviderMessageId;
        }
        else if (dispatch?.IsDeferred == true)
        {
            // Held back by the channel (the address is at its sending limit): the outbox sends it on its turn.
            state.NextAttemptUtc = dispatch.RetryAfterUtc;
            state.LastError = dispatch.GetErrorText();

            message.DeliveryStatus = MessageDeliveryStatus.Queued.ToString();
        }
        else
        {
            // Refused, and not retried: an acknowledgement that arrives an hour later, after a person may have answered,
            // would only confuse the contact. The bubble shows the failure instead.
            state.Attempts = 1;
            state.LastError = dispatch?.GetErrorText();

            message.DeliveryStatus = MessageDeliveryStatus.Failed.ToString();
        }

        message.ErrorCode = state.LastError;
        message.Put(state);

        return message;
    }
}
