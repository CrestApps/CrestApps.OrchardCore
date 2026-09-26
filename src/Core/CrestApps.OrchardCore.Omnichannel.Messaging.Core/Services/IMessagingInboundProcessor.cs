using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Turns a received message, on any channel, into (or appends it to) a human conversation and routes it to an owner.
/// </summary>
public interface IMessagingInboundProcessor
{
    /// <summary>
    /// Processes a received inbound message: find-or-create the conversation, run the routing chain, link the
    /// message, roll up the thread, and notify. Returns the conversation, or <see langword="null"/> when the
    /// message is not owned by the workspace (no enabled channel or no matching endpoint) or is being handled by the automated path.
    /// </summary>
    /// <param name="message">The received, persisted inbound message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The conversation the message was routed to, or <see langword="null"/>.</returns>
    Task<MessagingConversation> ProcessAsync(OmnichannelMessage message, CancellationToken cancellationToken = default);
}
