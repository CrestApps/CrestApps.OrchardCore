using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ActivityReservationIndex"/>.
/// </summary>
internal sealed class ActivityReservationIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the reservation index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ActivityReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("ExpiresUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ActivityReservationIndex>(table => table
            .CreateIndex("IDX_ActivityReservationIndex_DocumentId", "DocumentId", "AgentId", "Status", "ExpiresUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
