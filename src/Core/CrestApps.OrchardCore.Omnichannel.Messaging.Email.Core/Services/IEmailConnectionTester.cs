using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Checks an address's mail server settings before they are relied on: connects and signs in, without sending or
/// receiving anything.
/// </summary>
public interface IEmailConnectionTester
{
    /// <summary>
    /// Connects and signs in to an SMTP server.
    /// </summary>
    /// <param name="server">The SMTP server settings, with the password protected.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>An error to show, or <see langword="null"/> when the server accepted the sign-in.</returns>
    Task<string> TestSmtpAsync(EmailServerSettings server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects and signs in to an IMAP server and opens the mailbox's folders.
    /// </summary>
    /// <param name="mailbox">The mailbox settings, with the password protected.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>An error to show, or <see langword="null"/> when the mailbox opened.</returns>
    Task<string> TestImapAsync(EmailMailboxSettings mailbox, CancellationToken cancellationToken = default);
}
