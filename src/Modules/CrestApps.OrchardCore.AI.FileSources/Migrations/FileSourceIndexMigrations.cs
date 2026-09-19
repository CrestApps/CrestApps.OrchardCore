using CrestApps.Core.AI.FileSources;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.FileSources;
using CrestApps.Core.Data.YesSql.Indexes.Knowledge;
using CrestApps.Core.Data.YesSql.Indexes.WebCrawlers;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.FileSources.Migrations;

/// <summary>
/// Creates the YesSql index tables the file source stores read, and moves records that predate the split
/// between file sources and web crawlers.
/// </summary>
/// <remarks>
/// <para>
/// A file source is its own record in its own table. The knowledge index holds the typed pieces an ingested
/// file becomes, and the crawler tables are still created here because the framework's scheduler reads the
/// crawler store even on a tenant that has no web crawlers.
/// </para>
/// <para>
/// The knowledge and crawler tables are shared with the Web Crawlers feature, and either feature may be
/// enabled without the other or before it. Creating a table that is already there is a failure, not a
/// no-op, so those run through a builder that reports rather than throws -- otherwise enabling the second
/// of the two features logs an error and abandons the rest of its migration. The file source tables belong
/// to this feature alone, but they are created the same way so that a tenant upgrading from before the
/// split, which already ran <c>CreateAsync</c>, does not fail on the second table in the list.
/// </para>
/// </remarks>
internal sealed class FileSourceIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;
    private readonly IStore _store;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSourceIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The YesSql store options.</param>
    /// <param name="store">The YesSql store, which carries the configuration a schema builder needs.</param>
    /// <param name="serviceProvider">The tenant services, used to move records that predate the split.</param>
    public FileSourceIndexMigrations(
        IOptions<YesSqlStoreOptions> option,
        IStore store,
        IServiceProvider serviceProvider)
    {
        _option = option.Value;
        _store = store;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates every index table this feature reads.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await CreateSchemaAsync();

        return 2;
    }

    /// <summary>
    /// Adds the file source tables to a tenant that was set up before file sources had their own, and moves
    /// the records that were stored as web crawlers into them.
    /// </summary>
    /// <remarks>
    /// The move goes through the framework's own migration, which works through the store interfaces rather
    /// than SQL and is idempotent, so a tenant that has nothing to move simply finds nothing.
    /// </remarks>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateSchemaAsync();

        await _serviceProvider.MigrateFileSourcesAsync();

        return 2;
    }

    /// <summary>
    /// Creates the schemas, tolerating a table another feature or an earlier version already created.
    /// </summary>
    private async Task CreateSchemaAsync()
    {
        // Shares the migration's own transaction, so this is still one unit of work; it differs only in
        // tolerating a table that is already there.
        var builder = new SchemaBuilder(_store.Configuration, SchemaBuilder.Transaction, throwOnError: false);

        await builder.CreateFileSourceIndexSchemaAsync(_option);
        await builder.CreateIngestionItemStateIndexSchemaAsync(_option);
        await builder.CreateKnowledgeObjectIndexSchemaAsync(_option);

        // Read by the framework's scheduler even here, where there may be no web crawlers at all.
        await builder.CreateWebCrawlerIndexSchemaAsync(_option);
        await builder.CreateWebCrawlStateIndexSchemaAsync(_option);
    }
}
