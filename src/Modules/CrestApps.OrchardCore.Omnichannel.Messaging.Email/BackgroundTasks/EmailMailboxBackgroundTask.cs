using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.BackgroundTasks;

/// <summary>
/// Reads, every minute, the mailbox of each email address that receives its mail over IMAP, for the mail hosts that
/// cannot post inbound mail to a webhook.
/// </summary>
[BackgroundTask(
    Title = "Email Mailbox Reader",
    Schedule = "* * * * *",
    Description = "Receives new mail from the mailboxes of the email addresses that read their mailbox over IMAP.",
    LockTimeout = 5_000,
    LockExpiration = 600_000)]
public sealed class EmailMailboxBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var poller = serviceProvider.GetRequiredService<IEmailMailboxPoller>();
        var logger = serviceProvider.GetRequiredService<ILogger<EmailMailboxBackgroundTask>>();

        try
        {
            await poller.PollAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Reading the email mailboxes failed.");
        }
    }
}
