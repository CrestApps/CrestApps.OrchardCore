using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.Core.ContactCenter.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterOutboxMessageIndex"/>.
/// </summary>
internal sealed class ContactCenterOutboxMessageIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutboxMessageIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterOutboxMessageIndexMigrationsSchemaMigration(
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
    public string Name => "ContactCenterOutboxMessageIndexMigrations";

    /// <summary>
    /// Creates the outbox message index table and its supporting index.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EventId", column => column.WithLength(26))
            .Column<OutboxMessageStatus>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime>("CreatedUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .CreateIndex("IDX_ContactCenterOutboxMessageIndex_Due",
                "DocumentId",
                "Status",
                "NextAttemptUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
            .CreateIndex("IDX_ContactCenterOutboxMessageIndex_Retention",
                "Status",
                "CreatedUtc",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 2;
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
                    // Adds the creation time settled messages are purged by. The retry time cannot serve: a settled message
                    // keeps whatever retry time it last held, so it is not an age.
                    await builder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
                        .AddColumn<DateTime>("CreatedUtc"),
                        collection: ContactCenterStorage.CollectionName);

                    await ContactCenterMigrationSql.AddRetentionColumnAsync(
                        builder,
                        _store,
                        typeof(ContactCenterOutboxMessageIndex),
                        "CreatedUtc",
                        _timeProvider.GetUtcNow().UtcDateTime);

                    await builder.AlterIndexTableAsync<ContactCenterOutboxMessageIndex>(table => table
                        .CreateIndex("IDX_ContactCenterOutboxMessageIndex_Retention",
                            "Status",
                            "CreatedUtc",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 2;
                }
}
