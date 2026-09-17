using CrestApps.OrchardCore.Telnyx.Core.Migrations;
using CrestApps.OrchardCore.Telnyx.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Telnyx.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="TelnyxAgentCredentialIndex"/> that tracks browser SIP
/// credential ownership, the Telnyx credential id, expiry, and revocation per tenant.
/// </summary>
/// <remarks>
/// The schema itself lives in <see cref="TelnyxAgentCredentialMigrationsSchemaMigration"/>; this class only
/// hands Orchard's schema builder to it. The type name is kept because Orchard records the applied version
/// under this class's full type name, so renaming it would run the create step against existing tables.
/// </remarks>
public sealed class TelnyxAgentCredentialMigrations : DataMigration
{
    private readonly TelnyxAgentCredentialMigrationsSchemaMigration _step = new();

    /// <summary>
    /// Creates the credential index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
