using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

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
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .CreateIndex("IDX_CallbackRequestIndex_DocumentId", "DocumentId", "ItemId", "Status", "ScheduledUtc"),
            collection: ContactCenterConstants.CollectionName
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
            collection: ContactCenterConstants.CollectionName
        );

        return 3;
    }

    /// <summary>
    /// Adds the promotion lease column to existing callback request index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .AddColumn<DateTime>("LeaseExpiresUtc", column => column.Nullable()),
            collection: ContactCenterConstants.CollectionName
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
            collection: ContactCenterConstants.CollectionName
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
            collection: ContactCenterConstants.CollectionName
        );

        return 3;
    }
}
