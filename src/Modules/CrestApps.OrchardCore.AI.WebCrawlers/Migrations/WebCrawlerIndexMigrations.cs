using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.WebCrawlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Migrations;

/// <summary>
/// Creates the YesSql index tables backing the web-crawler stores.
/// </summary>
/// <remarks>
/// These tables belong to this feature alone. They are still created tolerantly because a tenant set up
/// under an earlier build has them already: File Sources used to store its records in the same index and
/// created them itself. Creating a table that is already there is a failure, not a no-op, so enabling this
/// feature on such a tenant would otherwise log an error and abandon the rest of its migration.
/// </remarks>
internal sealed class WebCrawlerIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;
    private readonly ILogger<WebCrawlerIndexMigrations> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCrawlerIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The YesSql store options.</param>
    /// <param name="store">The YesSql store, which carries the configuration a schema builder needs.</param>
    public WebCrawlerIndexMigrations(
        IOptions<YesSqlStoreOptions> option,
        ILogger<WebCrawlerIndexMigrations> logger
        )
    {
        _option = option.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates the web crawler and web crawl-state index schemas.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        try
        {
            await SchemaBuilder.CreateWebCrawlerIndexSchemaAsync(_option);
            await SchemaBuilder.CreateWebCrawlStateIndexSchemaAsync(_option);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create web crawler index schemas. It may be already exists.. you can ignore this warning.");
        }

        return 1;
    }
}
