using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using YesSql;
using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterEventMetricIndex"/>.
/// </summary>
public sealed class ContactCenterEventMetricIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventMetricIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterEventMetricIndexMigrationsSchemaMigration(IStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "ContactCenterEventMetricIndexMigrations";

    /// <summary>
    /// Creates the event metric index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DateKey", column => column.NotNull().WithLength(10))
            .Column<DateTime>("Date")
            .Column<string>("EventType", column => column.NotNull().WithLength(128)),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .CreateIndex("IDX_ContactCenterEventMetricIndex_DocumentId", "DocumentId", "DateKey", "Date", "EventType"),
            collection: ContactCenterStorage.CollectionName
        );

        await CreateMetricUniquenessConstraintAsync(builder);

        await builder.AlterIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterEventMetricIndex_Retention",
                "Date",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

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

    private async Task CreateMetricUniquenessConstraintAsync(ISchemaBuilder builder)
    {
        var tableName = ContactCenterMigrationSql.GetQuotedTableName(
            builder,
            _store,
            typeof(ContactCenterEventMetricIndex));
        var dateKeyColumn = builder.Dialect.QuoteForColumnName("DateKey");
        var eventTypeColumn = builder.Dialect.QuoteForColumnName("EventType");
        var duplicateExists = await ContactCenterMigrationSql.ExistsAsync(
            builder,
            $"SELECT 1 FROM {tableName} WHERE {dateKeyColumn} IS NOT NULL AND {eventTypeColumn} IS NOT NULL " +
            $"GROUP BY {dateKeyColumn}, {eventTypeColumn} HAVING COUNT(*) > 1");

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "The Contact Center event metric index contains multiple rows for the same date and event type. Resolve the duplicate legacy metrics before enabling metric uniqueness.");
        }

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(ContactCenterEventMetricIndex),
            "UQ_ContactCenterEventMetricIndex_DateEvent",
            "DateKey",
            "EventType");
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
                    // Adds the portable uniqueness constraint for an existing event-metric index.
                    await CreateMetricUniquenessConstraintAsync(builder);

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
    private static async Task<int> UpdateFrom2Async(ISchemaBuilder builder)
    {
                    // Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
                    // is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
                    await builder.AlterIndexTableAsync<ContactCenterEventMetricIndex>(table => table
                        .CreateIndex(
                            "IDX_ContactCenterEventMetricIndex_Retention",
                            "Date",
                            "DocumentId"),
                        collection: ContactCenterStorage.CollectionName);

                    return 3;
                }
}
