using CrestApps.OrchardCore.Consent.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Consent.Migrations;

/// <summary>
/// Creates the index of the opt-outs for one purpose recorded against an address.
/// </summary>
internal sealed class ConsentAddressOptOutMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table and its lookup by address, channel and purpose.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ConsentAddressOptOutIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("Address", column => column.WithLength(255))
            .Column<string>("Purpose", column => column.WithLength(100))
        );

        await SchemaBuilder.AlterIndexTableAsync<ConsentAddressOptOutIndex>(table => table
            .CreateIndex(
                "IDX_ConsentAddressOptOutIndex_Address",
                "Address",
                "Channel",
                "Purpose")
        );

        await SchemaBuilder.AlterIndexTableAsync<ConsentAddressOptOutIndex>(table => table
            .CreateIndex(
                "IDX_ConsentAddressOptOutIndex_ItemId",
                "ItemId",
                "DocumentId")
        );

        return 1;
    }
}
