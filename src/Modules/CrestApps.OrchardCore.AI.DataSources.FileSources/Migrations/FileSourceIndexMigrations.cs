using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Knowledge;
using CrestApps.Core.Data.YesSql.Indexes.WebCrawlers;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Migrations;

/// <summary>
/// Creates the YesSql index tables the file source stores read.
/// </summary>
/// <remarks>
/// A file source is stored as a <c>WebCrawler</c> record, distinguished from a web crawler only by its
/// source, so it shares that index; the knowledge index holds the typed pieces an ingested file becomes.
/// Both creations are idempotent, which is what lets this feature and Web Crawlers each be enabled without
/// the other.
/// </remarks>
internal sealed class FileSourceIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSourceIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The YesSql store options.</param>
    public FileSourceIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates the file source and knowledge index schemas.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateWebCrawlerIndexSchemaAsync(_option);
        await SchemaBuilder.CreateWebCrawlStateIndexSchemaAsync(_option);
        await SchemaBuilder.CreateKnowledgeObjectIndexSchemaAsync(_option);

        return 1;
    }
}
