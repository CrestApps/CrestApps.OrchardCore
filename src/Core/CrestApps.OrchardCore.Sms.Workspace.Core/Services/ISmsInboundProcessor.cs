using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// Turns a received SMS into (or appends it to) a human conversation and routes it to an owner.
/// </summary>
public interface ISmsInboundProcessor
{
    /// <summary>
    /// Processes a received inbound message: find-or-create the conversation, run the routing chain, link the
    /// message, roll up the thread, and notify. Returns the conversation, or <see langword="null"/> when the
    /// message is not owned by the portal (unknown DID) or is being handled by the automated path.
    /// </summary>
    /// <param name="message">The received, persisted inbound message.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The conversation the message was routed to, or <see langword="null"/>.</returns>
    Task<SmsConversation> ProcessAsync(OmnichannelMessage message, CancellationToken cancellationToken = default);
}
