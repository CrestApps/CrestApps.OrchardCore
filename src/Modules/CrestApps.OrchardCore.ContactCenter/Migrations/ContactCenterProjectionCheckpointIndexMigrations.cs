using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterProjectionCheckpointIndex"/> and enforces a single
/// checkpoint per projection handler through a unique constraint.
/// </summary>
internal sealed class ContactCenterProjectionCheckpointIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProjectionCheckpointIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterProjectionCheckpointIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the projection-checkpoint index table and its per-handler uniqueness constraint.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterProjectionCheckpointIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("HandlerId", column => column.NotNull().WithLength(128))
            .Column<int>("Version"),
            collection: ContactCenterConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterProjectionCheckpointIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterProjectionCheckpointIndex_Handler",
                "HandlerId",
                "DocumentId"),
            collection: ContactCenterConstants.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ContactCenterProjectionCheckpointIndex),
            "UQ_ContactCenterProjectionCheckpointIndex_Handler",
            "HandlerId");

        return 1;
    }
}
