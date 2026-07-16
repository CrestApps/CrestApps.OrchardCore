using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentProfileIndex"/>.
/// </summary>
internal sealed class AgentProfileIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the agent profile index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<AgentPresenceStatus>("PresenceStatus"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentProfileIndex>(table => table
            .CreateIndex("IDX_AgentProfileIndex_DocumentId", "DocumentId", "ItemId", "UserId", "PresenceStatus"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
