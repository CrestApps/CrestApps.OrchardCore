using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="SecureCaptureSessionIndex"/>.
/// </summary>
internal sealed class SecureCaptureSessionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the secure capture session index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SecureCaptureSessionIndex>(table => table
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

        await SchemaBuilder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Token", "AccessTokenHash", "State"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Expiry", "State", "ExpiresUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Interaction", "InteractionId", "State", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Resume", "EngagedRecordingPause", "RecordingResumed", "State", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex("IDX_SecureCaptureSessionIndex_Retention", "State", "ModifiedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex(
                "IDX_SecureCaptureSessionIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 2;
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<SecureCaptureSessionIndex>(table => table
            .CreateIndex(
                "IDX_SecureCaptureSessionIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 2;
    }
}
