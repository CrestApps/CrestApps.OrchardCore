using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterEventMetricIndex"/>.
/// </summary>
internal sealed class ContactCenterEventMetricIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventMetricIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterEventMetricIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the event metric index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DateKey", column => column.NotNull().WithLength(10))
            .Column<DateTime>("Date")
            .Column<string>("EventType", column => column.NotNull().WithLength(128)),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterEventMetricIndex>(table => table
            .CreateIndex("IDX_ContactCenterEventMetricIndex_DocumentId", "DocumentId", "DateKey", "Date", "EventType"),
            collection: ContactCenterConstants.CollectionName
        );

        await CreateMetricUniquenessConstraintAsync();

        return 2;
    }

    /// <summary>
    /// Adds the portable uniqueness constraint for an existing event-metric index.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateMetricUniquenessConstraintAsync();

        return 2;
    }

    private async Task CreateMetricUniquenessConstraintAsync()
    {
        var tableName = ContactCenterMigrationSql.GetQuotedTableName(
            SchemaBuilder,
            _store,
            typeof(ContactCenterEventMetricIndex));
        var dateKeyColumn = SchemaBuilder.Dialect.QuoteForColumnName("DateKey");
        var eventTypeColumn = SchemaBuilder.Dialect.QuoteForColumnName("EventType");
        var duplicateExists = await ContactCenterMigrationSql.ExistsAsync(
            SchemaBuilder,
            $"SELECT 1 FROM {tableName} WHERE {dateKeyColumn} IS NOT NULL AND {eventTypeColumn} IS NOT NULL " +
            $"GROUP BY {dateKeyColumn}, {eventTypeColumn} HAVING COUNT(*) > 1");

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "The Contact Center event metric index contains multiple rows for the same date and event type. Resolve the duplicate legacy metrics before enabling metric uniqueness.");
        }

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ContactCenterEventMetricIndex),
            "UQ_ContactCenterEventMetricIndex_DateEvent",
            "DateKey",
            "EventType");
    }
}
