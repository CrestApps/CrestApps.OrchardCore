using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tickets.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Tickets.BackgroundTasks;

/// <summary>
/// Takes the daily backlog snapshot of each desk. It runs every hour so a site that was down at midnight still records
/// the day; each desk's day is taken once.
/// </summary>
[BackgroundTask(
    Title = "Record the ticket backlog",
    Schedule = "5 * * * *",
    Description = "Records each desk's open tickets once a day, so the backlog can be reported for past days.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
public sealed class TicketBacklogBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<TicketBacklogRecorder>().RecordAsync(cancellationToken), "desks recorded"));
}
