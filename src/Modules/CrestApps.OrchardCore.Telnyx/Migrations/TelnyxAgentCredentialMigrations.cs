using CrestApps.OrchardCore.Telnyx.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telnyx.Migrations;

/// <summary>
/// Creates the schema for the durable <see cref="TelnyxAgentCredentialIndex"/> that tracks browser SIP
/// credential ownership, the Telnyx credential id, expiry, and revocation per tenant.
/// </summary>
public sealed class TelnyxAgentCredentialMigrations : DataMigration
{
    /// <summary>
    /// Creates the credential index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
            .Column<string>("TenantName", column => column.WithLength(255))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CredentialId", column => column.WithLength(128))
            .Column<string>("SipUsername", column => column.WithLength(128))
            .Column<DateTime>("ExpiresUtc")
            .Column<bool>("Revoked")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
            .CreateIndex("IDX_TelnyxAgentCredentialIndex_User",
                "UserId",
                "Revoked",
                "ExpiresUtc",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
            .CreateIndex("IDX_TelnyxAgentCredentialIndex_Credential",
                "CredentialId",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelnyxAgentCredentialIndex>(table => table
            .CreateIndex("IDX_TelnyxAgentCredentialIndex_Cleanup",
                "Revoked",
                "ExpiresUtc",
                "DocumentId")
        );

        return 1;
    }
}
