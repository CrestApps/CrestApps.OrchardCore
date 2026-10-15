using CrestApps.OrchardCore.Telephony.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Creates the schema for the <see cref="TelephonyExtensionIndex"/>.
/// </summary>
internal sealed class TelephonyExtensionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the telephony extension index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TelephonyExtensionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Number", column => column.WithLength(64))
            .Column<string>("UserId", column => column.WithLength(26))
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyExtensionIndex>(table => table
            .CreateIndex("IDX_TelephonyExtensionIndex_Number", "Number", "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyExtensionIndex>(table => table
            .CreateIndex("IDX_TelephonyExtensionIndex_UserId", "UserId", "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyExtensionIndex>(table => table
            .CreateIndex(
                "IDX_TelephonyExtensionIndex_ItemId",
                "ItemId",
                "DocumentId"));

        return 2;
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<TelephonyExtensionIndex>(
            SchemaBuilder,
            "IDX_TelephonyExtensionIndex_ItemId",
            null,
            "ItemId", "DocumentId");

        return 2;
    }
}
