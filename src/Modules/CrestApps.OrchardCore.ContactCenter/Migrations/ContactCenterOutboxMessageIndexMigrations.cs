using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql.Sql;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterOutboxMessageIndex"/>.
/// </summary>
internal sealed class ContactCenterOutboxMessageIndexMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutboxMessageIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterOutboxMessageIndexMigrations(
        IStore store,
        IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Creates the outbox message index table and its supporting index.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EventId", column => column.WithLength(26))
            .Column<OutboxMessageStatus>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime>("CreatedUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .CreateIndex("IDX_ContactCenterOutboxMessageIndex_Due",
                "DocumentId",
                "Status",
                "NextAttemptUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .CreateIndex("IDX_ContactCenterOutboxMessageIndex_Retention",
                "Status",
                "CreatedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterOutboxMessageIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 3;
    }

    /// <summary>
    /// Adds the creation time settled messages are purged by. The retry time cannot serve: a settled message
    /// keeps whatever retry time it last held, so it is not an age.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .AddColumn<DateTime>("CreatedUtc"),
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.AddRetentionColumnAsync(
            SchemaBuilder,
            _store,
            typeof(ContactCenterOutboxMessageIndex),
            "CreatedUtc",
            _clock.UtcNow);

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .CreateIndex("IDX_ContactCenterOutboxMessageIndex_Retention",
                "Status",
                "CreatedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 2;
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<ContactCenterOutboxMessageIndex>(
            SchemaBuilder,
            "IDX_ContactCenterOutboxMessageIndex_ItemId",
            ContactCenterStorage.CollectionName,
            "ItemId", "DocumentId");

        return 3;
    }

    /// <summary>
    /// Adds the index a message is found by from the event it carries. Every event raised on the call path is
    /// checked against the outbox by its event identifier, and no index leads with <c>EventId</c>, so each such
    /// read scanned the table. The index is not unique: rows written before it existed were never checked against
    /// one another, and a constraint they violate would fail the tenant's activation.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<ContactCenterOutboxMessageIndex>(
            SchemaBuilder,
            "IDX_ContactCenterOutboxMessageIndex_EventId",
            ContactCenterStorage.CollectionName,
            "EventId", "DocumentId");

        return 4;
    }
}
