using CrestApps.OrchardCore.Telephony.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Creates the schema used to resolve provider events to connected Orchard users.
/// </summary>
public sealed class TelephonyUserConnectionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TelephonyUserConnectionIndex>(table => table
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("RemoteUserId", column => column.WithLength(64))
            .Column<string>("NormalizedRemoteUserEmail", column => column.WithLength(255))
            .Column<string>("NormalizedRemotePhoneNumber", column => column.WithLength(64))
            .Column<bool>("IsEnabled")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyUserConnectionIndex>(table => table
            .CreateIndex(
                "IDX_TelephonyUserConnectionIndex_RemoteUserId",
                "ProviderName",
                "RemoteUserId",
                "IsEnabled",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyUserConnectionIndex>(table => table
            .CreateIndex(
                "IDX_TelephonyUserConnectionIndex_Email",
                "ProviderName",
                "NormalizedRemoteUserEmail",
                "IsEnabled",
                "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<TelephonyUserConnectionIndex>(table => table
            .CreateIndex(
                "IDX_TelephonyUserConnectionIndex_Phone",
                "ProviderName",
                "NormalizedRemotePhoneNumber",
                "IsEnabled",
                "DocumentId")
        );

        return 1;
    }
}
