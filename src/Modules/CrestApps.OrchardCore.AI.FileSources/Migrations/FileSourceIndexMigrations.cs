using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.FileSources;
using CrestApps.Core.Data.YesSql.Indexes.Knowledge;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

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
/// The knowledge table is shared with the Web Crawlers feature, and either feature may be enabled without
/// the other or before it. Creating a table that is already there is a failure, not a no-op, so these run
/// through a builder that reports rather than throws -- otherwise enabling the second of the two features
/// logs an error and abandons the rest of its migration.
/// </para>
/// <para>
/// The crawler tables belong to the Web Crawlers feature and are created by its own migration. This feature
/// does not read them.
/// </para>
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
    /// Creates every index table this feature reads.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateFileSourceIndexSchemaAsync(_option);
        await SchemaBuilder.CreateIngestionItemStateIndexSchemaAsync(_option);
        await SchemaBuilder.CreateKnowledgeObjectIndexSchemaAsync(_option);

        return 1;
    }
}
