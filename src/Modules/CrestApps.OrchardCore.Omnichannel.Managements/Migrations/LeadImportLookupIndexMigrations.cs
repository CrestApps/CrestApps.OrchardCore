using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Adds the indexes the reads of one import's leads seek on the lead import index.
/// </summary>
/// <remarks>
/// The entry id index carries neither the lead's document nor its id, and a file imports tens of thousands of leads, so
/// a read of one file's leads looked every row up in the table and the database chose to read the whole table instead.
/// The count of each import's leads filters on the latest version, which that index carries last. Kept apart from
/// <see cref="CrmMigrations"/> so the step does not share a version number with that class's own steps.
/// </remarks>
internal sealed class LeadImportLookupIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the indexes when they are missing. The lead import table is created by <see cref="CrmMigrations"/>,
    /// which runs first.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        // An import's leads, and the import's file name read from one of them.
        await IndexTableIndexCreation.CreateIfMissingAsync<LeadImportIndex>(
            SchemaBuilder,
            "IDX_LeadImportIndex_EntryLeads",
            collection: null,
            "EntryId", "Latest", "ContentType", "Published", "ContentItemId", "DocumentId");

        // The number of latest leads of each import.
        await IndexTableIndexCreation.CreateIfMissingAsync<LeadImportIndex>(
            SchemaBuilder,
            "IDX_LeadImportIndex_LatestEntry",
            collection: null,
            "Latest", "EntryId");

        return 1;
    }
}
