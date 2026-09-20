using CrestApps.Core.ContactCenter;
using System.Globalization;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="CallbackRequestIndex"/>.
/// </summary>
public sealed class CallbackRequestIndexMigrationsSchemaMigration : ISchemaMigration
{
    private static readonly string _terminalStatusValues = string.Join(
        ", ",
        ((int)CallbackRequestStatus.Scheduled).ToString(CultureInfo.InvariantCulture),
        ((int)CallbackRequestStatus.Completed).ToString(CultureInfo.InvariantCulture),
        ((int)CallbackRequestStatus.Canceled).ToString(CultureInfo.InvariantCulture),
        ((int)CallbackRequestStatus.Failed).ToString(CultureInfo.InvariantCulture));

    private readonly IStore _store;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequestIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The document store, used to resolve the physical table name.</param>
    /// <param name="timeProvider">The time provider used to date the retention backfill.</param>
    public CallbackRequestIndexMigrationsSchemaMigration(
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
    public string Name => "CallbackRequestIndexMigrations";

    /// <summary>
    /// Creates the callback request index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<CallbackRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<CallbackRequestStatus>("Status")
            .Column<DateTime>("ScheduledUtc")
            .Column<DateTime>("LeaseExpiresUtc", column => column.Nullable())
            .Column<DateTime>("ModifiedUtc", column => column.Nullable()),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .CreateIndex("IDX_CallbackRequestIndex_DocumentId", "DocumentId", "ItemId", "Status", "ScheduledUtc"),
            collection: ContactCenterStorage.CollectionName
        );

        // Adding a column does not re-project rows that already exist, so the pre-upgrade backlog would keep
        // a null modification time and could never be purged.
        await ContactCenterMigrationSql.AddRetentionColumnAsync(
            builder,
            _store,
            typeof(CallbackRequestIndex),
            "ModifiedUtc",
            _timeProvider.GetUtcNow().UtcDateTime,
            $"{builder.Dialect.QuoteForColumnName("Status")} IN ({_terminalStatusValues})");

        await builder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
            .CreateIndex("IDX_CallbackRequestIndex_Retention", "Status", "ModifiedUtc", "DocumentId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 3;
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

            case 2:
                return await UpdateFrom2Async(builder);

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
    private static async Task<int> UpdateFrom1Async(ISchemaBuilder builder)
    {
                    // Adds the promotion lease column to existing callback request index tables.
                    await builder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
                        .AddColumn<DateTime>("LeaseExpiresUtc", column => column.Nullable()),
                        collection: ContactCenterStorage.CollectionName
                    );

                    return 2;
                }

    /// <summary>
    /// Moves the schema from version 2 to the next version.
    /// </summary>
    /// <remarks>
    /// Kept as its own method, under the name the Orchard migration used, because the additive-only
    /// guard authorizes destructive steps per method. Folding every version into one switch would let
    /// a single authorization cover them all.
    /// </remarks>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    private async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the last-modified time settled callbacks are purged by. The scheduled time cannot serve: a callback
                    // booked weeks ahead and then canceled keeps a future scheduled time, so it would never look old enough.
                    await builder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
                        .AddColumn<DateTime>("ModifiedUtc", column => column.Nullable()),
                        collection: ContactCenterStorage.CollectionName
                    );

                    // Adding a column does not re-project rows that already exist, so the pre-upgrade backlog would keep
                    // a null modification time and could never be purged.
                    await ContactCenterMigrationSql.AddRetentionColumnAsync(
                        builder,
                        _store,
                        typeof(CallbackRequestIndex),
                        "ModifiedUtc",
                        _timeProvider.GetUtcNow().UtcDateTime,
                        $"{builder.Dialect.QuoteForColumnName("Status")} IN ({_terminalStatusValues})");

                    await builder.AlterIndexTableAsync<CallbackRequestIndex>(table => table
                        .CreateIndex("IDX_CallbackRequestIndex_Retention", "Status", "ModifiedUtc", "DocumentId"),
                        collection: ContactCenterStorage.CollectionName
                    );

                    return 3;
                }
}
