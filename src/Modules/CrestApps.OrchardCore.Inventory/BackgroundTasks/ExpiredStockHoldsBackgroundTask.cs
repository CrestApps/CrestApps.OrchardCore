using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Inventory.BackgroundTasks;

/// <summary>
/// Every five minutes, lets go of stock held for orders that weren't paid in time.
/// </summary>
[BackgroundTask(
    Title = "Expired stock holds",
    Schedule = "*/5 * * * *",
    Description = "Releases stock held for orders that weren't paid before the hold expired.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class ExpiredStockHoldsBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 500;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ExpiredStockHoldsBackgroundTask>>();

        var released = await serviceProvider.GetRequiredService<IInventoryService>().ReleaseExpiredAsync(BatchSize, cancellationToken);

        if (released > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Released {Count} expired stock holds.", released);
        }
    }
}
