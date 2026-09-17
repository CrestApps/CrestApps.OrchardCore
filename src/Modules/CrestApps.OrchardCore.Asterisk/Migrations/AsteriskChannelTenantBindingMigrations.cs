using CrestApps.OrchardCore.Asterisk.Migrations.Steps;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Asterisk.Migrations;

/// <summary>
/// Creates the schema for durable per-tenant Asterisk channel ownership bindings.
/// </summary>
/// <remarks>
/// The schema itself lives in <see cref="AsteriskChannelTenantBindingMigrationsSchemaMigration"/>; this class
/// only hands Orchard's schema builder to it. The type name is kept because Orchard records the applied version
/// under this class's full type name, so renaming it would run the create step against existing tables.
/// </remarks>
internal sealed class AsteriskChannelTenantBindingMigrations : DataMigration
{
    private readonly AsteriskChannelTenantBindingMigrationsSchemaMigration _step = new();

    /// <summary>
    /// Creates the channel tenant binding index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the peer channel column and its index so either leg's terminal event can release the whole call
    /// through an indexed reverse lookup.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Drops the channel tenant binding index table.
    /// </summary>
    /// <returns>A task that completes when the table has been dropped.</returns>
    public Task UninstallAsync()
        => AsteriskChannelTenantBindingMigrationsSchemaMigration.UninstallAsync(SchemaBuilder);
}
