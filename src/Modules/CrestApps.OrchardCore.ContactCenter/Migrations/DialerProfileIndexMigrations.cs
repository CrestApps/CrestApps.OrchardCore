using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="DialerProfileIndex"/>. A dialer profile is reusable dialing settings:
/// it does not own a campaign or queue (those are chosen when inventory is loaded), so the index carries only the
/// name and enabled flag used to list and pace profiles.
/// </summary>
internal sealed class DialerProfileIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the dialer profile index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DialerProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<bool>("Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<DialerProfileIndex>(table => table
            .CreateIndex("IDX_DialerProfileIndex_DocumentId", "DocumentId", "ItemId", "Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }
}
