using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.FileSources;
using CrestApps.Core.Data.YesSql.Indexes.Knowledge;
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
/// Creating the tables is all there is to do. File sources became their own records in their own tables
/// before anyone was storing any, so there is nothing of an earlier shape to carry forward.
/// </para>
/// <para>
/// The crawler tables belong to the Web Crawlers feature and are created by its own migration. This feature
/// does not read them.
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
    /// Creates every index table this feature reads.
    /// </summary>
    /// <remarks>
    /// Not the inherited <see cref="DataMigration.SchemaBuilder"/>, which throws. The knowledge table is
    /// shared with the Web Crawlers feature and either feature may be enabled first, so whichever runs
    /// second meets a table that is already there — and creating one that exists is a failure, not a no-op.
    /// On the strict builder that failure abandons the rest of the method, which is how a tenant ends up
    /// with no <c>AI_FileSourceIndex</c> at all and an admin screen that cannot open.
    /// <para>
    /// The feature's own two tables are created tolerantly for the same reason rather than because they are
    /// shared: a provider that commits each schema change on its own, as MySQL does, leaves the earlier
    /// tables behind when a later one fails, so a retry would then fail on the first instead.
    /// </para>
    /// </remarks>
    public async Task<int> CreateAsync()
    {
        // Shares the migration's own transaction, so this is still one unit of work; it differs only in
        // tolerating a table that is already there.
        var builder = new SchemaBuilder(_store.Configuration, SchemaBuilder.Transaction, throwOnError: false);

        await builder.CreateFileSourceIndexSchemaAsync(_option);
        await builder.CreateIngestionItemStateIndexSchemaAsync(_option);
        await builder.CreateKnowledgeObjectIndexSchemaAsync(_option);

        return 1;
    }
}
