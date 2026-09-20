using CrestApps.Core.ContactCenter;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterWorkStateIndex"/>.
/// </summary>
internal sealed class ContactCenterWorkStateIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterWorkStateIndexMigrationsSchemaMigration(
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
    public string Name => "ContactCenterWorkStateIndexMigrations";

    /// <summary>
    /// Creates the work state index table.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<ContactCenterWorkStateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.NotNull().WithDefault(string.Empty).WithLength(26))
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<string>("AssignedToId", column => column.WithLength(26)),
            collection: ContactCenterStorage.CollectionName
        );

        await builder.AlterIndexTableAsync<ContactCenterWorkStateIndex>(table => table
            .CreateIndex("IDX_ContactCenterWorkStateIndex_DocumentId", "DocumentId", "AssignmentStatus", "AssignedToId", "ReservedById"),
            collection: ContactCenterStorage.CollectionName
        );

        // One activity may only ever have one routing work state row. Without this constraint a concurrent
        // first-touch on two nodes would create two authorities for the same work item and routing would
        // silently reserve it twice.
        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(ContactCenterWorkStateIndex),
            "UQ_ContactCenterWorkStateIndex_ActivityItemId",
            "ActivityItemId");

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
                    // Adds the modification time the work state is purged by. Nothing in the product ever deletes a work
                    // state, so without an age this table grows by one row for every activity that is ever routed.
                    await builder.AlterIndexTableAsync<ContactCenterWorkStateIndex>(table => table
                        .AddColumn<DateTime>("ModifiedUtc", column => column.Nullable()),
                        collection: ContactCenterStorage.CollectionName
                    );

                    // Adding a column does not re-project rows that already exist, so without this every work state that
                    // predates the upgrade would keep a null age and could never be purged.
                    await ContactCenterMigrationSql.AddRetentionColumnAsync(
                        builder,
                        _store,
                        typeof(ContactCenterWorkStateIndex),
                        "ModifiedUtc",
                        _timeProvider.GetUtcNow().UtcDateTime);

                    await builder.AlterIndexTableAsync<ContactCenterWorkStateIndex>(table => table
                        .CreateIndex("IDX_ContactCenterWorkStateIndex_Retention", "ModifiedUtc", "DocumentId"),
                        collection: ContactCenterStorage.CollectionName
                    );

                    return 2;
                }
}
