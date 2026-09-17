using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Telephony.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telephony.Core.Migrations;

/// <summary>
/// Creates the schema used to resolve provider events to connected Orchard users.
/// </summary>
public sealed class TelephonyUserConnectionIndexSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The recorded name is the Orchard migration class this step was lifted out of, so a database
    /// migrated under either host agrees on which version is already applied.
    /// </remarks>
    public string Name => "TelephonyUserConnectionIndexMigrations";

    /// <inheritdoc/>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<TelephonyUserConnectionIndex>(table => table
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("RemoteUserId", column => column.WithLength(64))
            .Column<string>("NormalizedRemoteUserEmail", column => column.WithLength(255))
            .Column<string>("NormalizedRemotePhoneNumber", column => column.WithLength(64))
            .Column<bool>("IsEnabled")
        );

        await builder.AlterIndexTableAsync<TelephonyUserConnectionIndex>(table => table
            .CreateIndex(
                "IDX_TelephonyUserConnectionIndex_RemoteUserId",
                "ProviderName",
                "RemoteUserId",
                "IsEnabled",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelephonyUserConnectionIndex>(table => table
            .CreateIndex(
                "IDX_TelephonyUserConnectionIndex_Email",
                "ProviderName",
                "NormalizedRemoteUserEmail",
                "IsEnabled",
                "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelephonyUserConnectionIndex>(table => table
            .CreateIndex(
                "IDX_TelephonyUserConnectionIndex_Phone",
                "ProviderName",
                "NormalizedRemotePhoneNumber",
                "IsEnabled",
                "DocumentId")
        );

        return 1;
    }

    /// <inheritdoc/>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
        => Task.FromResult(version);
}
