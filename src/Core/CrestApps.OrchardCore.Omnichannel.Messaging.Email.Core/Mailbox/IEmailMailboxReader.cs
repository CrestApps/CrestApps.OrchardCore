using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;

/// <summary>
/// Reads the new mail in one email address's mailbox and hands each email to the inbound receiver. This is how a mail
/// host that cannot call a webhook (Microsoft 365, Google Workspace, any IMAP host) reaches the workspace.
/// </summary>
public interface IEmailMailboxReader
{
    /// <summary>
    /// Reads the mail that arrived in an address's mailbox since the last read.
    /// </summary>
    /// <param name="address">The address whose mailbox is read.</param>
    /// <param name="settings">The address's email settings, with the mailbox connection.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What the read found.</returns>
    Task<EmailMailboxReadResult> ReadAsync(OmnichannelChannelEndpoint address, EmailAddressSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to a mailbox and opens its folder without reading anything, to check the settings.
    /// </summary>
    /// <param name="mailbox">The mailbox settings.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>An error to show, or <see langword="null"/> when the mailbox opened.</returns>
    Task<string> TestAsync(EmailMailboxSettings mailbox, CancellationToken cancellationToken = default);
}
