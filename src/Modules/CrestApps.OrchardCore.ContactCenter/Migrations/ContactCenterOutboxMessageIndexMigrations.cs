using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

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

        return 2;
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
}
