using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentSessionIndex"/>.
/// </summary>
internal sealed class AgentSessionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the agent session index table and its supporting index.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<bool>("IsOnline")
            .Column<DateTime>("LastHeartbeatUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentSessionIndex>(table => table
            .CreateIndex("IDX_AgentSessionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "UserId",
                "IsOnline",
                "LastHeartbeatUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
