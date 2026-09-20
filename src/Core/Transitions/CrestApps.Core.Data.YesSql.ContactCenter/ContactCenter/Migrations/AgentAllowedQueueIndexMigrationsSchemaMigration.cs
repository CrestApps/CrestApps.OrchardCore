using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Services;
using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentAllowedQueueIndex"/>.
/// </summary>
public sealed class AgentAllowedQueueIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "AgentAllowedQueueIndexMigrations";

    /// <summary>
    /// Creates the agent allowed-queue index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<AgentAllowedQueueIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26)),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<AgentAllowedQueueIndex>(table => table
            .CreateIndex(
                "IDX_AgentAllowedQueueIndex_Queue",
                "DocumentId",
                "QueueId",
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
