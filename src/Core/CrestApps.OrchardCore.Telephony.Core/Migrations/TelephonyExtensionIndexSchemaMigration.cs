using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.Telephony.Core.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telephony.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="TelephonyExtensionIndex"/>.
/// </summary>
public sealed class TelephonyExtensionIndexSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The recorded name is the Orchard migration class this step was lifted out of, so a database
    /// migrated under either host agrees on which version is already applied.
    /// </remarks>
    public string Name => "TelephonyExtensionIndexMigrations";

    /// <inheritdoc/>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<TelephonyExtensionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Number", column => column.WithLength(64))
            .Column<string>("UserId", column => column.WithLength(26))
        );

        await builder.AlterIndexTableAsync<TelephonyExtensionIndex>(table => table
            .CreateIndex("IDX_TelephonyExtensionIndex_Number", "Number", "DocumentId")
        );

        await builder.AlterIndexTableAsync<TelephonyExtensionIndex>(table => table
            .CreateIndex("IDX_TelephonyExtensionIndex_UserId", "UserId", "DocumentId")
        );

        return 1;
    }

    /// <inheritdoc/>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
        => Task.FromResult(version);
}
