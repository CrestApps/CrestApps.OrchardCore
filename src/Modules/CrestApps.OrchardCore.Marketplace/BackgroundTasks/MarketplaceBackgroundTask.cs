using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketplace.BackgroundTasks;

/// <summary>
/// Every hour: releases vendors' earnings whose hold ended, and pays them out when the store pays out by itself.
/// </summary>
[BackgroundTask(
    Title = "Marketplace releases and payouts",
    Schedule = "30 * * * *",
    Description = "Releases vendors' earnings whose hold ended and whose orders are fulfilled, and pays them out when payouts are automatic.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class MarketplaceBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var marketplace = serviceProvider.GetRequiredService<MarketplaceService>();

        await marketplace.ReleaseDueAsync(500, cancellationToken);

        if ((await serviceProvider.GetRequiredService<ISiteService>().GetSettingsAsync<MarketplaceSettings>()).AutomaticPayouts && marketplace.CanPayOut)
        {
            await marketplace.PayOutAllAsync(cancellationToken);
        }
    }
}
