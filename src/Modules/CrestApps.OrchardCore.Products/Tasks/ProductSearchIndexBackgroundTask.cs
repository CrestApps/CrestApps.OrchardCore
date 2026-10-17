using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Products.Tasks;

/// <summary>
/// Rebuilds the product search records when they no longer match the product definitions.
/// </summary>
/// <remarks>
/// Publishing keeps single records current. This task catches what publishing cannot: the feature being
/// enabled on a site that already has products, a field being marked filterable, or a product type's sales
/// mode changing. It compares a fingerprint of those definitions with the one the records were built from,
/// so an unchanged site costs one small query a minute.
/// </remarks>
[BackgroundTask(
    Title = "Product Search Indexing",
    Schedule = "* * * * *",
    Description = "Rebuilds the product search index when product types or their attributes change.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class ProductSearchIndexBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var indexer = serviceProvider.GetRequiredService<ProductSearchIndexer>();
        var fingerprint = await indexer.GetFingerprintAsync();

        if (string.Equals(fingerprint, await indexer.GetSavedFingerprintAsync(), StringComparison.Ordinal))
        {
            return;
        }

        var logger = serviceProvider.GetRequiredService<ILogger<ProductSearchIndexBackgroundTask>>();
        var shellHost = serviceProvider.GetRequiredService<IShellHost>();
        var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();

        logger.LogInformation("Rebuilding the product search index because product types or their attributes changed.");

        var count = await ProductSearchIndexer.RebuildAsync(shellHost, shellSettings, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await indexer.SaveStateAsync(fingerprint);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Rebuilt the product search index with {Count} products.", count);
        }
    }
}
