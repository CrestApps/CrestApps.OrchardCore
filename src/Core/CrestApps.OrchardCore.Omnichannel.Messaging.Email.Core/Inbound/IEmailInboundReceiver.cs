namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// Receives one email for the business, whichever source it came from: finds the address it was sent to, keeps its files,
/// separates the reply from the quoted history, recognises bounces and automatic mail, and commits it to the durable
/// provider inbox, from which the workspace and automation receive it. A new inbound source (a provider's push API, for
/// example) only has to produce an <see cref="InboundEmail"/> and call <see cref="ReceiveAsync"/>.
/// </summary>
public interface IEmailInboundReceiver
{
    /// <summary>
    /// Receives one email.
    /// </summary>
    /// <param name="email">The parsed email.</param>
    /// <param name="source">The source the email came from, such as <c>imap</c> or <c>sendgrid</c>, for the logs.</param>
    /// <param name="dispatch">Whether the email is processed now, once it is committed to the inbox. A webhook that answers
    /// its caller first passes <see langword="false"/> and dispatches <see cref="EmailInboundResult.InboxMessageId"/> after.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What became of the email.</returns>
    Task<EmailInboundResult> ReceiveAsync(InboundEmail email, string source, bool dispatch = true, CancellationToken cancellationToken = default);
}
