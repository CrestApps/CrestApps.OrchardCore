using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Asterisk.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Asterisk.Migrations.Steps;

/// <summary>
/// Creates the schema for durable per-tenant Asterisk channel ownership bindings.
/// </summary>
internal sealed class AsteriskChannelTenantBindingMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "AsteriskChannelTenantBindingMigrations";

    /// <summary>
    /// Creates the channel tenant binding index table and its supporting indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .Column<string>("ChannelId", column => column.WithLength(256))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("PeerChannelId", column => column.WithLength(256))
        );

        await builder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .CreateIndex("IDX_AsteriskChannelTenantBindingIndex_ChannelId",
                "ChannelId",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .CreateIndex("IDX_AsteriskChannelTenantBindingIndex_Provider",
                "ProviderName",
                "InteractionId",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .CreateIndex("IDX_AsteriskChannelTenantBindingIndex_PeerChannelId",
                "PeerChannelId",
                "DocumentId")
        );

        return 2;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                // Adds the peer channel column and its index so either leg's terminal event can release the
                // whole call through an indexed reverse lookup.
                await builder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
                    .AddColumn<string>("PeerChannelId", column => column.WithLength(256))
                );

                await builder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
                    .CreateIndex("IDX_AsteriskChannelTenantBindingIndex_PeerChannelId",
                        "PeerChannelId",
                        "DocumentId")
                );

                return 2;

            default:
                return version;
        }
    }

    /// <summary>
    /// Drops the channel tenant binding index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>A task that completes when the table has been dropped.</returns>
    public static async Task UninstallAsync(ISchemaBuilder builder)
    {
        await builder.DropMapIndexTableAsync<AsteriskChannelTenantBindingIndex>();
    }
}
