using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tickets.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Tickets.BackgroundTasks;

/// <summary>
/// Tries again the requester e-mails that couldn't be sent.
/// </summary>
[BackgroundTask(
    Title = "Send ticket e-mails",
    Schedule = "* * * * *",
    Description = "Tries again the e-mails to requesters that couldn't be sent, and gives up after six tries.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class TicketEmailOutboxBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<TicketEmailOutbox>().SendDueAsync(cancellationToken), "sent"));
}
