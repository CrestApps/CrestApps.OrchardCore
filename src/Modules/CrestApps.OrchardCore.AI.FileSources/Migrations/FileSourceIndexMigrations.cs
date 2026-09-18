using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.Knowledge;
using CrestApps.Core.Data.YesSql.Indexes.WebCrawlers;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.FileSources.Migrations;

/// <summary>
/// Creates the YesSql index tables the file source stores read.
/// </summary>
/// <remarks>
/// <para>
/// A file source is stored as a <c>WebCrawler</c> record, distinguished from a web crawler only by its
/// source, so it shares that index; the knowledge index holds the typed pieces an ingested file becomes.
/// </para>
/// <para>
/// Both tables are shared with the Web Crawlers feature, and either feature may be enabled without the
/// other or before it. Creating a table that is already there is a failure, not a no-op, so these run
/// through a builder that reports rather than throws -- otherwise enabling the second of the two features
/// logs an error and abandons the rest of its migration.
/// </para>
/// </remarks>
internal sealed class FileSourceIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSourceIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The YesSql store options.</param>
    /// <param name="store">The YesSql store, which carries the configuration a schema builder needs.</param>
    public FileSourceIndexMigrations(IOptions<YesSqlStoreOptions> option, IStore store)
    {
        _option = option.Value;
        _store = store;
    }

    /// <summary>
    /// Creates the file source and knowledge index schemas.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        // Shares the migration's own transaction, so this is still one unit of work; it differs only in
        // tolerating a table another feature already created.
        var builder = new SchemaBuilder(_store.Configuration, SchemaBuilder.Transaction, throwOnError: false);

        await builder.CreateWebCrawlerIndexSchemaAsync(_option);
        await builder.CreateWebCrawlStateIndexSchemaAsync(_option);
        await builder.CreateKnowledgeObjectIndexSchemaAsync(_option);

        return 1;
    }
}
