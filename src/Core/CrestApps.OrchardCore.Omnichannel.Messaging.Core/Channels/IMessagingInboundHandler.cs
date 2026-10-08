namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// Takes part in every inbound message the workspace receives, so a channel can apply the rules that belong to it
/// alone, such as the carrier keywords (STOP, START, HELP) of a text message, without the workspace knowing about
/// them. Handlers run in ascending <see cref="Order"/> and should return early for a channel they do not serve.
/// </summary>
public interface IMessagingInboundHandler
{
    /// <summary>
    /// Gets the order the handler runs in. Lower runs first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Runs before the conversation is routed. A handler that recognises a message automation must not answer
    /// (a compliance keyword, for example) sets <see cref="MessagingInboundContext.SuppressAutomatedReplies"/>.
    /// </summary>
    /// <param name="context">The inbound message and its conversation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReceivingAsync(MessagingInboundContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs after the conversation is routed and before it is saved, so changes made to the conversation here are
    /// persisted with the message.
    /// </summary>
    /// <param name="context">The inbound message and its conversation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReceivedAsync(MessagingInboundContext context, CancellationToken cancellationToken = default);
}
