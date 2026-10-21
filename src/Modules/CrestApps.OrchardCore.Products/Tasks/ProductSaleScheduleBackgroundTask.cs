using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Products.Tasks;

/// <summary>
/// Builds a product's search record again when its sale starts or ends, so listings and its page show the sale price
/// only while it runs.
/// </summary>
/// <remarks>
/// The window it looks back over starts a day back after a restart, so a sale that started or ended while the site
/// was down is caught up; building a record again is harmless.
/// </remarks>
[BackgroundTask(
    Title = "Product Sales",
    Schedule = "* * * * *",
    Description = "Updates listings when a product's sale starts or ends.",
    LockTimeout = 3_000,
    LockExpiration = 120_000)]
public sealed class ProductSaleScheduleBackgroundTask : IBackgroundTask
{
    private DateTime? _checkedUntilUtc;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var now = serviceProvider.GetRequiredService<IClock>().UtcNow;
        var from = _checkedUntilUtc ?? now.AddDays(-1);

        var session = serviceProvider.GetRequiredService<ISession>();
        var changed = await session.QueryIndex<ProductSaleIndex>(index =>
                (index.StartsUtc > from && index.StartsUtc <= now) ||
                (index.EndsUtc > from && index.EndsUtc <= now))
            .ListAsync(cancellationToken);

        var ids = changed.Select(index => index.ContentItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();

        if (ids.Count > 0)
        {
            var contentManager = serviceProvider.GetRequiredService<IContentManager>();
            var indexer = serviceProvider.GetRequiredService<ProductSearchIndexer>();

            foreach (var item in await contentManager.GetAsync(ids, VersionOptions.Published))
            {
                await indexer.IndexAsync(item);
            }

            var logger = serviceProvider.GetRequiredService<ILogger<ProductSaleScheduleBackgroundTask>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Updated the listings of {Count} products whose sale started or ended.", ids.Count);
            }
        }

        _checkedUntilUtc = now;
    }
}
