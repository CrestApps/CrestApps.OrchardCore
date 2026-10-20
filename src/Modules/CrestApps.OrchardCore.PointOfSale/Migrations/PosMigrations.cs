using CrestApps.OrchardCore.PointOfSale.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.PointOfSale.Migrations;

/// <summary>
/// Creates the indexes of registers and their shifts.
/// </summary>
public sealed class PosMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<RegisterIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("LocationId", column => column.WithLength(26))
            .Column<bool>("IsEnabled"),
            collection: PosConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<RegisterShiftIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("RegisterId", column => column.WithLength(26))
            .Column<string>("LocationId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("OpenedUtc"),
            collection: PosConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<RegisterIndex>(SchemaBuilder, "IDX_RegisterIndex_ItemId", PosConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<RegisterShiftIndex>(SchemaBuilder, "IDX_RegisterShiftIndex_ItemId", PosConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<RegisterShiftIndex>(SchemaBuilder, "IDX_RegisterShiftIndex_Register", PosConstants.CollectionName, "RegisterId", "Status");

        return 1;
    }
}
