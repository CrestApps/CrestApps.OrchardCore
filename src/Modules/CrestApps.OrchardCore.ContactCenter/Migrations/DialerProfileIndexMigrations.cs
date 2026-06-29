using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="DialerProfileIndex"/>.
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
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<bool>("Enabled"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<DialerProfileIndex>(table => table
            .CreateIndex("IDX_DialerProfileIndex_DocumentId", "DocumentId", "CampaignId", "QueueId", "Enabled"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
