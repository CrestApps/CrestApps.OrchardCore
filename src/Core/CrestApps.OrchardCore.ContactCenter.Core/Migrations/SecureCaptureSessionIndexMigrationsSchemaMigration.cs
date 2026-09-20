using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="SecureCaptureSessionIndex"/>.
/// </summary>
internal sealed class SecureCaptureSessionIndexMigrationsSchemaMigration : ISchemaMigration
{
    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "SecureCaptureSessionIndexMigrations";

    /// <summary>
    /// Creates the secure capture session index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<int>("State")
            .Column<bool>("EngagedRecordingPause")
            .Column<bool>("RecordingResumed")
            .Column<string>("AccessTokenHash", column => column.WithLength(64))
            .Column<DateTime>("ExpiresUtc")
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Token", "AccessTokenHash", "State"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Expiry", "State", "ExpiresUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Interaction", "InteractionId", "State", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Resume", "EngagedRecordingPause", "RecordingResumed", "State", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Retention", "State", "ModifiedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        return Task.FromResult(version);
    }
}
