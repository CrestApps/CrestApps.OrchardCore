using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tickets.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Tickets.BackgroundTasks;

/// <summary>
/// Solves the open incidents of solved problems that the request solving the problem left, a few hundred a run.
/// </summary>
[BackgroundTask(
    Title = "Solve incidents of solved problems",
    Schedule = "*/5 * * * *",
    Description = "Solves the open incidents of solved problems beyond the ones solved with the problem.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
public sealed class TicketProblemIncidentsBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<TicketProblemCascade>().RunAsync(cancellationToken), "solved"));
}
