using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterProcessedEventIndex"/> and enforces per-handler
/// event idempotency through a composite unique constraint.
/// </summary>
internal sealed class ContactCenterProcessedEventIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessedEventIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterProcessedEventIndexMigrationsSchemaMigration(
        IStore store,
        TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "ContactCenterProcessedEventIndexMigrations";

    /// <summary>
    /// Creates the processed-event index table and its per-handler event uniqueness constraint.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("HandlerId", column => column.NotNull().WithLength(128))
            .Column<string>("EventId", column => column.NotNull().WithLength(26)),
            collection: ContactCenterStorage.CollectionName);

        await builder.AlterIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterProcessedEventIndex_Handler",
                "HandlerId",
                "EventId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
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
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public async Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        switch (version)
        {
            case 1:
                return await UpdateFrom1Async(builder);

            default:
                return version;
        }
    }

    /// <summary>
    /// Moves the schema from version 1 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds the processed time these markers are purged by, and gives rows that predate the column one full
                    // retention window rather than the default instant, which every cutoff is newer than.
                    await builder.AlterIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
                        .AddColumn<DateTime>("ProcessedUtc"),
                        collection: ContactCenterStorage.CollectionName);

                    await ContactCenterMigrationSql.AddRetentionColumnAsync(
                        builder,
                        _store,
                        typeof(ContactCenterProcessedEventIndex),
                        "ProcessedUtc",
                        _timeProvider.GetUtcNow().UtcDateTime);

                    await builder.AlterIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
                        .CreateIndex(
                            "IDX_ContactCenterProcessedEventIndex_Retention",
                            "ProcessedUtc",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 2;
                }
}
