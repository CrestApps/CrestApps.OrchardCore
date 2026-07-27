using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterWorkStateIndex"/>.
/// </summary>
internal sealed class ContactCenterWorkStateIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterWorkStateIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the work state index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContactCenterWorkStateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.NotNull().WithDefault(string.Empty).WithLength(26))
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<string>("AssignedToId", column => column.WithLength(26)),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<ContactCenterWorkStateIndex>(table => table
            .CreateIndex("IDX_ContactCenterWorkStateIndex_DocumentId", "DocumentId", "AssignmentStatus", "AssignedToId", "ReservedById"),
            collection: ContactCenterConstants.CollectionName
        );

        // One activity may only ever have one routing work state row. Without this constraint a concurrent
        // first-touch on two nodes would create two authorities for the same work item and routing would
        // silently reserve it twice.
        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(ContactCenterWorkStateIndex),
            "UQ_ContactCenterWorkStateIndex_ActivityItemId",
            "ActivityItemId");

        return 1;
    }
}
