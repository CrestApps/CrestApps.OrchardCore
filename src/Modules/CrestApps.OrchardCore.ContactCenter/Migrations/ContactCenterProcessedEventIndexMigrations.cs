using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterProcessedEventIndex"/> and enforces per-handler
/// event idempotency through a composite unique constraint.
/// </summary>
internal sealed class ContactCenterProcessedEventIndexMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessedEventIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterProcessedEventIndexMigrations(
        IStore store,
        IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Creates the processed-event index table and its per-handler event uniqueness constraint.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("HandlerId", column => column.NotNull().WithLength(128))
            .Column<string>("EventId", column => column.NotNull().WithLength(26)),
            collection: ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterProcessedEventIndex_Handler",
                "HandlerId",
                "EventId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ContactCenterProcessedEventIndex),
            "UQ_ContactCenterProcessedEventIndex_Handler",
            "HandlerId",
            "EventId");

        // The retention column is left to the update step. Declaring it here as well would put this table on
        // the synthesised upgrade path, where the unique constraint this create step makes with raw SQL cannot
        // be reproduced, and a fresh installation would then stop enforcing what an upgraded one enforces.
        return 1;
    }

    /// <summary>
    /// Adds the processed time these markers are purged by, and gives rows that predate the column one full
    /// retention window rather than the default instant, which every cutoff is newer than.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
            .AddColumn<DateTime>("ProcessedUtc"),
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.AddRetentionColumnAsync(
            SchemaBuilder,
            _store,
            typeof(ContactCenterProcessedEventIndex),
            "ProcessedUtc",
            _clock.UtcNow);

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterProcessedEventIndex_Retention",
                "ProcessedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 2;
    }
}
