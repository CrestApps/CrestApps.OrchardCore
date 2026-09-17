using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Asterisk.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Asterisk.Migrations.Steps;

/// <summary>
/// Creates the schema for the durable <see cref="AsteriskPjsipCredentialLeaseIndex"/> that tracks browser
/// SIP credential ownership, expiry, and revocation per tenant. This is a schema migration for a new
/// durable store and is expected; it does not alter any existing data.
/// </summary>
internal sealed class AsteriskPjsipCredentialLeaseMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "AsteriskPjsipCredentialLeaseMigrations";

    /// <summary>
    /// Creates the credential lease index table and its supporting indexes.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .Column<string>("AuthorizationUser", column => column.WithLength(128))
            .Column<string>("TenantName", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(128))
            .Column<DateTime>("ExpiresUtc")
            .Column<bool>("Revoked")
        );

        await builder.AlterIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .CreateIndex("IDX_AsteriskPjsipCredentialLeaseIndex_AuthorizationUser",
                "AuthorizationUser",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .CreateIndex("IDX_AsteriskPjsipCredentialLeaseIndex_User",
                "UserId",
                "Revoked",
                "ExpiresUtc",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .CreateIndex("IDX_AsteriskPjsipCredentialLeaseIndex_Cleanup",
                "Revoked",
                "ExpiresUtc",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .CreateIndex("IDX_AsteriskPjsipCredentialLeaseIndex_Session",
                "SessionId",
                "Revoked",
                "DocumentId")
        );

        return 1;
    }

    /// <summary>
    /// Moves the schema forward from an already-applied version. There is no step past the create, so the
    /// applied version is returned unchanged.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
        => Task.FromResult(version);
}
