using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tickets.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Tickets.BackgroundTasks;

/// <summary>
/// Asks requesters to rate their solved tickets once their desk's delay has passed.
/// </summary>
[BackgroundTask(
    Title = "Ask requesters to rate solved tickets",
    Schedule = "20 * * * *",
    Description = "E-mails the requester of a solved ticket a link to rate it, once, when the desk's rating delay has passed.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
public sealed class TicketRatingRequestBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<TicketRatingRequests>().SendDueAsync(cancellationToken), "asked"));
}
