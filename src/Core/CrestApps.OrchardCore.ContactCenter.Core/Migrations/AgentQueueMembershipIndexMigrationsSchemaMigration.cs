using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentQueueMembershipIndex"/>.
/// </summary>
internal sealed class AgentQueueMembershipIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "AgentQueueMembershipIndexMigrations";

    /// <summary>
    /// Creates the agent queue membership index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<AgentQueueMembershipIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<AgentPresenceStatus>("PresenceStatus")
            .Column<int>("MaxConcurrentInteractions"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<AgentQueueMembershipIndex>(table => table
            .CreateIndex(
                "IDX_AgentQueueMembershipIndex_Queue",
                "DocumentId",
                "QueueId",
                "PresenceStatus",
                "ItemId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        return Task.FromResult(version);
    }
}
