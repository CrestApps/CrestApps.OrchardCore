using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Composes an outbound email from a workspace message and sends it through the transport of the address it leaves from:
/// it threads a reply to the customer's last email, adds the signature and, on bulk and outreach mail, the unsubscribe
/// link, and attaches the files from the attachment store.
/// </summary>
public interface IEmailDispatcher
{
    /// <summary>
    /// Sends one message as an email.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome, with the email's <c>Message-ID</c> when the transport set it. A refusal is returned, never
    /// thrown.</returns>
    Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage message, CancellationToken cancellationToken = default);
}
