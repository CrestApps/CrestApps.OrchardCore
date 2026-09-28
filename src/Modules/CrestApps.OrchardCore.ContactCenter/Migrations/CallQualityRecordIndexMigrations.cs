using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallQualityRecordIndex"/>.
/// </summary>
internal sealed class CallQualityRecordIndexMigrations : DataMigration
{
    // The source name, a separator and a provider call-control id, which is sized like a provider call id.
    private const int RecordKeyLength = 300;

    /// <summary>
    /// Creates the call quality record index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CallQualityRecordIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("RecordKey", column => column.WithLength(RecordKeyLength))
            .Column<CallQualitySource>("Source")
            .Column<CallQualityRating>("Rating")
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<DateTime>("ObservedUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallQualityRecordIndex>(table => table
            .CreateIndex(
                "IDX_CallQualityRecordIndex_RecordKey",
                "RecordKey",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallQualityRecordIndex>(table => table
            .CreateIndex(
                "IDX_CallQualityRecordIndex_Retention",
                "ObservedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallQualityRecordIndex>(table => table
            .CreateIndex(
                "IDX_CallQualityRecordIndex_Agent",
                "AgentId",
                "ObservedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }
}
