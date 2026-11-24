using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tickets.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Tickets.BackgroundTasks;

/// <summary>
/// Closes solved tickets once their desk's auto-close delay has passed.
/// </summary>
[BackgroundTask(
    Title = "Close solved tickets",
    Schedule = "*/15 * * * *",
    Description = "Closes solved tickets once their desk's auto-close delay has passed since they were solved.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
public sealed class TicketAutoCloseBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            run.AddItems(await serviceProvider.GetRequiredService<TicketAutoCloser>().CloseDueAsync(cancellationToken: cancellationToken), "closed");

            // Then the protected information of tickets closed longer ago than their desk keeps it.
            run.AddItems(await serviceProvider.GetRequiredService<TicketProtectedInformationCleaner>().DeleteDueAsync(cancellationToken), "protected information deleted");
        });
}
