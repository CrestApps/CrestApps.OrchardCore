using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallQualityRecordIndex"/>.
/// </summary>
internal sealed class CallQualityRecordIndexMigrations : DataMigration
{
    // The source name, a separator and a provider call-control id, which is sized like a provider call id.
    private const int RecordKeyLength = 300;

    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallQualityRecordIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public CallQualityRecordIndexMigrations(IStore store)
    {
        _store = store;
    }

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
            .Column<string>("QueueId", column => column.WithLength(ContactCenterStorage.QueueIdLength))
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

    /// <summary>
    /// Widens the queue column so work routed under a campaign's virtual queue is stored rather than refused.
    /// </summary>
    /// <remarks>
    /// A campaign call's quality record carries the campaign's virtual queue it was routed under. A campaign's virtual queue id is longer than the original 26 characters, so SQL Server refused the
    /// row. SQLite stores every text column as unbounded <c>TEXT</c>, so the rebuild is a value-preserving no-op
    /// there. No index refers to the queue column, so nothing comes down around the rebuild.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await IndexStringColumnRebuild.WidenAsync<CallQualityRecordIndex>(
            SchemaBuilder,
            _store,
            "QueueId",
            ContactCenterStorage.QueueIdLength,
            isNotNull: false,
            defaultValue: null,
            ContactCenterStorage.CollectionName);

        return 2;
    }
}
