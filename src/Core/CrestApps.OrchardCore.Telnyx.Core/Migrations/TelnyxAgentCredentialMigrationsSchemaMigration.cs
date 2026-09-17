using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Telnyx.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telnyx.Core.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="TelnyxAgentCredentialIndex"/> that tracks browser SIP
/// credential ownership, the Telnyx credential id, expiry, and revocation per tenant.
/// </summary>
internal sealed class TelnyxAgentCredentialMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "TelnyxAgentCredentialMigrations";

    /// <summary>
    /// Creates the credential index table and its supporting indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
            .Column<string>("TenantName", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CredentialId", column => column.WithLength(128))
            .Column<string>("SipUsername", column => column.WithLength(128))
            .Column<DateTime>("ExpiresUtc")
            .Column<bool>("Revoked")
        );

        await builder.AlterIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
            .CreateIndex("IDX_TelnyxAgentCredentialIndex_User",
                "UserId",
                "Revoked",
                "ExpiresUtc",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
            .CreateIndex("IDX_TelnyxAgentCredentialIndex_Credential",
                "CredentialId",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
            .CreateIndex("IDX_TelnyxAgentCredentialIndex_Cleanup",
                "Revoked",
                "ExpiresUtc",
                "DocumentId")
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
