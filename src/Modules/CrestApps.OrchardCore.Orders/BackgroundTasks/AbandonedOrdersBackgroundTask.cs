using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Orders.BackgroundTasks;

/// <summary>
/// Every 15 minutes, cancels unpaid orders that nothing can pay for any more, so what they hold is let go.
/// </summary>
[BackgroundTask(
    Title = "Abandoned orders",
    Schedule = "*/15 * * * *",
    Description = "Cancels unpaid orders whose checkout was canceled, failed, expired or never started.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class AbandonedOrdersBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<AbandonedOrderSweeper>().SweepAsync(cancellationToken);
}
