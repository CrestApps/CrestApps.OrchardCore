using CrestApps.OrchardCore.PointOfSale.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.PointOfSale.Migrations;

/// <summary>
/// Creates the indexes of registers and their shifts.
/// </summary>
internal sealed class PosMigrations : DataMigration
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

        await SchemaBuilder.AlterIndexTableAsync<RegisterIndex>(table => table
            .CreateIndex("IDX_RegisterIndex_ItemId", "ItemId"),
            collection: PosConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<RegisterShiftIndex>(table =>
        {
            table.CreateIndex("IDX_RegisterShiftIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_RegisterShiftIndex_Register", "RegisterId", "Status");
        }, collection: PosConstants.CollectionName);

        return 1;
    }
}
