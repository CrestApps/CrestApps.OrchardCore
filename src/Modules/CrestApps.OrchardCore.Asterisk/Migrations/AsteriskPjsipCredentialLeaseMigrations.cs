using CrestApps.OrchardCore.Asterisk.Indexes;
using CrestApps.OrchardCore.Asterisk.Migrations.Steps;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Asterisk.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="AsteriskPjsipCredentialLeaseIndex"/> that tracks browser
/// SIP credential ownership, expiry, and revocation per tenant. This is a schema migration for a new
/// durable store and is expected; it does not alter any existing data.
/// </summary>
/// <remarks>
/// The schema itself lives in <see cref="AsteriskPjsipCredentialLeaseMigrationsSchemaMigration"/>; this class
/// only hands Orchard's schema builder to it. The type name is kept because Orchard records the applied version
/// under this class's full type name, so renaming it would run the create step against existing tables.
/// </remarks>
public sealed class AsteriskPjsipCredentialLeaseMigrations : DataMigration
{
    private readonly AsteriskPjsipCredentialLeaseMigrationsSchemaMigration _step = new();

    /// <summary>
    /// Creates the credential lease index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
