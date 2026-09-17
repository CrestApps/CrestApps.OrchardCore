using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="DialerProfileIndex"/>. A dialer profile is reusable dialing settings:
/// it does not own a campaign or queue (those are chosen when inventory is loaded), so the index carries only the
/// name and enabled flag used to list and pace profiles.
/// </summary>
internal sealed class DialerProfileIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "DialerProfileIndexMigrations";

    /// <summary>
    /// Creates the dialer profile index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<DialerProfileIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<bool>("Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<DialerProfileIndex>(table => table
            .CreateIndex("IDX_DialerProfileIndex_DocumentId", "DocumentId", "ItemId", "Enabled"),
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
