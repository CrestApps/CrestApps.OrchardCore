using CrestApps.Core.Omnichannel;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Omnichannel.Migrations;

/// <summary>
/// Creates the schema for the cadence index table that lists the re-engagement cadences a campaign can pick.
/// </summary>
public sealed class CadenceIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "CadenceIndexMigrations";

    /// <summary>
    /// Creates the cadence index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<CadenceIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<bool>("Enabled")
            .Column<DateTime>("CreatedUtc"),
        collection: OmnichannelCollections.Name
        );

        await builder.AlterIndexTableAsync<CadenceIndex>(table => table
            .CreateIndex("IDX_CadenceIndex_DocumentId",
                "DocumentId",
                "DisplayText",
                "ItemId"
            ),
        collection: OmnichannelCollections.Name
        );

        return 1;
    }

    /// <summary>
    /// Moves the schema forward from an already-applied version. There is no step past the create, so
    /// the applied version is returned unchanged.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
        => Task.FromResult(version);
}
