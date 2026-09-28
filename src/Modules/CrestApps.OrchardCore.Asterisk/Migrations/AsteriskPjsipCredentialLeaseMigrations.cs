using CrestApps.OrchardCore.Asterisk.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Asterisk.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="AsteriskPjsipCredentialLeaseIndex"/> that tracks browser
/// SIP credential ownership, expiry, and revocation per tenant. This is a schema migration for a new
/// durable store and is expected; it does not alter any existing data.
/// </summary>
public sealed class AsteriskPjsipCredentialLeaseMigrations : DataMigration
{
    /// <summary>
    /// Creates the credential lease index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .Column<string>("AuthorizationUser", column => column.WithLength(128))
            .Column<string>("TenantName", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(128))
            .Column<DateTime>("ExpiresUtc")
            .Column<bool>("Revoked")
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .CreateIndex("IDX_AsteriskPjsipCredentialLeaseIndex_AuthorizationUser",
                "AuthorizationUser",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .CreateIndex("IDX_AsteriskPjsipCredentialLeaseIndex_User",
                "UserId",
                "Revoked",
                "ExpiresUtc",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .CreateIndex("IDX_AsteriskPjsipCredentialLeaseIndex_Cleanup",
                "Revoked",
                "ExpiresUtc",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<AsteriskPjsipCredentialLeaseIndex>(table => table
            .CreateIndex("IDX_AsteriskPjsipCredentialLeaseIndex_Session",
                "SessionId",
                "Revoked",
                "DocumentId")
        );

        return 1;
    }
}
