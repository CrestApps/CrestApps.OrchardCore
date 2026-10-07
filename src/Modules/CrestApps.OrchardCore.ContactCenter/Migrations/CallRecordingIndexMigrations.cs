using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallRecordingIndex"/>.
/// </summary>
internal sealed class CallRecordingIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the call recording index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CallRecordingIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<CallRecordingSource>("Source")
            .Column<string>("ProviderRecordingId", column => column.WithLength(128))
            .Column<string>("ProviderCallId", column => column.WithLength(128))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("AgentUserId", column => column.WithLength(26))
            .Column<string>("CustomerAddress", column => column.WithLength(64))
            .Column<InteractionDirection>("Direction")
            .Column<DateTime>("StartedUtc")
            .Column<double>("DurationSeconds")
            .Column<bool>("IsStored")
            .Column<bool>("IsErased"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallRecordingIndex>(table => table
            .CreateIndex("IDX_CallRecordingIndex_DocumentId", "DocumentId", "ItemId", "ProviderRecordingId", "InteractionId"),
            collection: ContactCenterStorage.CollectionName
        );

        // The page lists the playable recordings newest first, usually one agent's.
        await SchemaBuilder.AlterIndexTableAsync<CallRecordingIndex>(table => table
            .CreateIndex("IDX_CallRecordingIndex_Agent", "IsStored", "IsErased", "AgentUserId", "StartedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallRecordingIndex>(table => table
            .CreateIndex("IDX_CallRecordingIndex_ProviderCall", "ProviderCallId", "ProviderRecordingId", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await CreateActivityIndexAsync();

        return 3;
    }

    /// <summary>
    /// Adds the recorded call leg, which a recording is listed under when it starts and matched by when it is saved.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<CallRecordingIndex>(table => table
            .AddColumn<string>("ProviderCallId", column => column.WithLength(128)),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallRecordingIndex>(table => table
            .CreateIndex("IDX_CallRecordingIndex_ProviderCall", "ProviderCallId", "ProviderRecordingId", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Indexes the CRM activity a recording belongs to, which an activity's page lists its call recordings by.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await CreateActivityIndexAsync();

        return 3;
    }

    private Task CreateActivityIndexAsync()
        => SchemaBuilder.AlterIndexTableAsync<CallRecordingIndex>(table => table
            .CreateIndex("IDX_CallRecordingIndex_Activity", "ActivityItemId", "IsStored", "IsErased", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );
}
