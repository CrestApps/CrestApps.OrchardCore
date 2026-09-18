using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.WebCrawlers;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Migrations;

/// <summary>
/// Creates the YesSql index tables backing the web-crawler stores.
/// </summary>
/// <remarks>
/// These tables are shared with the File Sources feature, which stores its records in the same index, and
/// either feature may be enabled without the other or before it. Creating a table that is already there is
/// a failure, not a no-op, so these run through a builder that reports rather than throws -- otherwise
/// enabling the second of the two features logs an error and abandons the rest of its migration.
/// </remarks>
internal sealed class WebCrawlerIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCrawlerIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The YesSql store options.</param>
    /// <param name="store">The YesSql store, which carries the configuration a schema builder needs.</param>
    public WebCrawlerIndexMigrations(IOptions<YesSqlStoreOptions> option, IStore store)
    {
        _option = option.Value;
        _store = store;
    }

    /// <summary>
    /// Creates the web crawler and web crawl-state index schemas.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        // Shares the migration's own transaction, so this is still one unit of work; it differs only in
        // tolerating a table the File Sources feature already created.
        var builder = new SchemaBuilder(_store.Configuration, SchemaBuilder.Transaction, throwOnError: false);

        await builder.CreateWebCrawlerIndexSchemaAsync(_option);
        await builder.CreateWebCrawlStateIndexSchemaAsync(_option);

        return 1;
    }
}
