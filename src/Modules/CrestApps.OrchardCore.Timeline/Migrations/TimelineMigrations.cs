using CrestApps.OrchardCore.Timeline.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Timeline.Migrations;

/// <summary>
/// Creates the index table of timeline entries.
/// </summary>
internal sealed class TimelineMigrations : DataMigration
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await TimelineIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Adds the external id an entry has outside the site, such as an e-mail's message id.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TimelineEntryIndex>(table =>
        {
            table.AddColumn<string>("ExternalId", column => column.Nullable().WithLength(TimelineConstants.MaxExternalIdLength));
            table.CreateIndex("IDX_TL_Entry_External", "ExternalId");
        }, collection: TimelineConstants.CollectionName);

        return 2;
    }
}
