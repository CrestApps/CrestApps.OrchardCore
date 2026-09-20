using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentStateReasonCodeIndex"/>.
/// </summary>
/// <remarks>
/// Only the schema. The Orchard migration that drives this also seeds the standard reason codes from a recipe,
/// and a recipe is an Orchard concept, so that half stays with the host. A host without recipes gets the table
/// and decides for itself what to put in it.
/// </remarks>
public sealed class AgentStateReasonCodeIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "AgentStateReasonCodeIndexMigrations";

    /// <summary>
    /// Creates the reason code index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<AgentStateReasonCodeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<int>("SortOrder")
            .Column<bool>("Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<AgentStateReasonCodeIndex>(table => table
            .CreateIndex("IDX_AgentStateReasonCodeIndex_DocumentId", "DocumentId", "ItemId", "Enabled"),
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
