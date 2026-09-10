using CrestApps.OrchardCore.Asterisk.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Asterisk.Migrations;

/// <summary>
/// Creates the schema for durable per-tenant Asterisk channel ownership bindings.
/// </summary>
internal sealed class AsteriskChannelTenantBindingMigrations : DataMigration
{
    /// <summary>
    /// Creates the channel tenant binding index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .Column<string>("ChannelId", column => column.WithLength(256))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("PeerChannelId", column => column.WithLength(256))
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .CreateIndex("IDX_AsteriskChannelTenantBindingIndex_ChannelId",
                "ChannelId",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .CreateIndex("IDX_AsteriskChannelTenantBindingIndex_Provider",
                "ProviderName",
                "InteractionId",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .CreateIndex("IDX_AsteriskChannelTenantBindingIndex_PeerChannelId",
                "PeerChannelId",
                "DocumentId")
        );

        return 2;
    }

    /// <summary>
    /// Adds the peer channel column and its index so either leg's terminal event can release the whole call
    /// through an indexed reverse lookup.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .AddColumn<string>("PeerChannelId", column => column.WithLength(256))
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskChannelTenantBindingIndex>(table => table
            .CreateIndex("IDX_AsteriskChannelTenantBindingIndex_PeerChannelId",
                "PeerChannelId",
                "DocumentId")
        );

        return 2;
    }

    /// <summary>
    /// Drops the channel tenant binding index table.
    /// </summary>
    /// <returns>A task that completes when the table has been dropped.</returns>
    public async Task UninstallAsync()
    {
        await SchemaBuilder.DropMapIndexTableAsync<AsteriskChannelTenantBindingIndex>();
    }
}
