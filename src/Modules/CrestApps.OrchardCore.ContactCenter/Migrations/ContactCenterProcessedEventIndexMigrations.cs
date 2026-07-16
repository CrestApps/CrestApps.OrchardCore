using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessedEventIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterProcessedEventIndexMigrations(IStore store)
    {
        _store = store;
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
            collection: ContactCenterConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterProcessedEventIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterProcessedEventIndex_Handler",
                "HandlerId",
                "EventId",
                "DocumentId"),
            collection: ContactCenterConstants.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ContactCenterProcessedEventIndex),
            "UQ_ContactCenterProcessedEventIndex_Handler",
            "HandlerId",
            "EventId");

        return 1;
    }
}
