namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;

/// <summary>
/// Reads the mailbox of every email address that receives its mail that way, once per pass.
/// </summary>
public interface IEmailMailboxPoller
{
    /// <summary>
    /// Reads each mailbox that is due.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many emails were received across the mailboxes.</returns>
    Task<int> PollAsync(CancellationToken cancellationToken = default);
}
