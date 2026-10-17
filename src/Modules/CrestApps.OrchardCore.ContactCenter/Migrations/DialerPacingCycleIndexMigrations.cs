using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="DialerPacingCycleIndex"/>.
/// </summary>
internal sealed class DialerPacingCycleIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the dialer pacing cycle index table and the indexes its latest cycle of a queue or profile, its period
    /// reads and its retention purge are found by.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DialerPacingCycleIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(ContactCenterStorage.QueueIdLength))
            .Column<string>("DialerProfileId", column => column.Nullable().WithLength(26))
            .Column<DialerPacingCycleOutcome>("Outcome")
            .Column<DateTime>("CompletedUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<DialerPacingCycleIndex>(table => table
            .CreateIndex(
                "IDX_DialerPacingCycleIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<DialerPacingCycleIndex>(table => table
            .CreateIndex(
                "IDX_DialerPacingCycleIndex_Queue",
                "QueueId",
                "CompletedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<DialerPacingCycleIndex>(table => table
            .CreateIndex(
                "IDX_DialerPacingCycleIndex_Profile",
                "DialerProfileId",
                "CompletedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<DialerPacingCycleIndex>(table => table
            .CreateIndex(
                "IDX_DialerPacingCycleIndex_Retention",
                "CompletedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }
}
