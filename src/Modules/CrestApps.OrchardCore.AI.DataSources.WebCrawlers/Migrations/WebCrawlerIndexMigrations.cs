using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.WebCrawlers;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.Migrations;

/// <summary>
/// Creates the YesSql index tables backing the web-crawler stores.
/// </summary>
internal sealed class WebCrawlerIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebCrawlerIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The YesSql store options.</param>
    public WebCrawlerIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates the web crawler and web crawl-state index schemas.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateWebCrawlerIndexSchemaAsync(_option);
        await SchemaBuilder.CreateWebCrawlStateIndexSchemaAsync(_option);

        return 1;
    }
}
