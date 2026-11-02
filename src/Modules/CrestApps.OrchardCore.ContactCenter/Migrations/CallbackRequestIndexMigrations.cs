using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql.Sql;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallbackRequestIndex"/>.
/// </summary>
internal sealed class CallbackRequestIndexMigrations : DataMigration
{
    private static readonly string _terminalStatusValues = string.Join(
        ", ",
        ((int)CallbackRequestStatus.Scheduled).ToString(CultureInfo.InvariantCulture),
        ((int)CallbackRequestStatus.Completed).ToString(CultureInfo.InvariantCulture),
        ((int)CallbackRequestStatus.Canceled).ToString(CultureInfo.InvariantCulture),
        ((int)CallbackRequestStatus.Failed).ToString(CultureInfo.InvariantCulture));

    private readonly IStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequestIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The document store, used to resolve the physical table name.</param>
    /// <param name="clock">The clock used to date the retention backfill.</param>
    public CallbackRequestIndexMigrations(
        IStore store,
        IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Creates the callback request index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CallbackRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<CallbackRequestStatus>("Status")
            .Column<DateTime>("ScheduledUtc")
            .Column<DateTime>("LeaseExpiresUtc", column => column.Nullable())
            .Column<DateTime>("ModifiedUtc", column => column.Nullable())
            .Column<string>("AgentId", column => column.Nullable().WithLength(ContactCenterStorage.AgentIdLength)),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .CreateIndex("IDX_CallbackRequestIndex_DocumentId", "DocumentId", "ItemId", "Status", "ScheduledUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        // Adding a column does not re-project rows that already exist, so the pre-upgrade backlog would keep
        // a null modification time and could never be purged.
        await ContactCenterMigrationSql.AddRetentionColumnAsync(
            SchemaBuilder,
            _store,
            typeof(CallbackRequestIndex),
            "ModifiedUtc",
            _clock.UtcNow,
            $"{SchemaBuilder.Dialect.QuoteForColumnName("Status")} IN ({_terminalStatusValues})");

        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .CreateIndex("IDX_CallbackRequestIndex_Retention", "Status", "ModifiedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .CreateIndex(
                "IDX_CallbackRequestIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await CreateReportIndexAsync();
        await CreateAgentIndexAsync();

        return 6;
    }

    /// <summary>
    /// Adds the promotion lease column to existing callback request index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .AddColumn<DateTime>("LeaseExpiresUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Adds the last-modified time settled callbacks are purged by. The scheduled time cannot serve: a callback
    /// booked weeks ahead and then canceled keeps a future scheduled time, so it would never look old enough.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .AddColumn<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName
        );

        // Adding a column does not re-project rows that already exist, so the pre-upgrade backlog would keep
        // a null modification time and could never be purged.
        await ContactCenterMigrationSql.AddRetentionColumnAsync(
            SchemaBuilder,
            _store,
            typeof(CallbackRequestIndex),
            "ModifiedUtc",
            _clock.UtcNow,
            $"{SchemaBuilder.Dialect.QuoteForColumnName("Status")} IN ({_terminalStatusValues})");

        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .CreateIndex("IDX_CallbackRequestIndex_Retention", "Status", "ModifiedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 3;
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<CallbackRequestIndex>(
            SchemaBuilder,
            "IDX_CallbackRequestIndex_ItemId",
            ContactCenterStorage.CollectionName,
            "ItemId", "DocumentId");

        return 4;
    }

    /// <summary>
    /// Adds the index reports read the records through, newest first by <c>ScheduledUtc</c> a page at a time, so
    /// each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await CreateReportIndexAsync();

        return 5;
    }

    /// <summary>
    /// Adds the agent a personal callback is reserved for, so the calendar finds an agent's open callbacks in a range
    /// without reading every callback. Callbacks saved before this step have no agent in the index until they are
    /// saved again.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .AddColumn<string>("AgentId", column => column.Nullable().WithLength(ContactCenterStorage.AgentIdLength)),
            collection: ContactCenterStorage.CollectionName);

        await CreateAgentIndexAsync();

        return 6;
    }

    private Task<bool> CreateAgentIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<CallbackRequestIndex>(
            SchemaBuilder,
            "IDX_CallbackRequestIndex_Agent",
            ContactCenterStorage.CollectionName,
            "AgentId", "ScheduledUtc", "Status");
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<CallbackRequestIndex>(
            SchemaBuilder,
            "IDX_CallbackRequestIndex_Report",
            ContactCenterStorage.CollectionName,
            "ScheduledUtc", "Id");
    }
}
