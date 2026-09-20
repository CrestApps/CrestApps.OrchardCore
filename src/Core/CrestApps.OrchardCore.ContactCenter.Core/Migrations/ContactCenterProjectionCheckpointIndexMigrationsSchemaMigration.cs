using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Migrations;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterProjectionCheckpointIndex"/> and enforces a single
/// checkpoint per projection handler through a unique constraint.
/// </summary>
internal sealed class ContactCenterProjectionCheckpointIndexMigrationsSchemaMigration : ISchemaMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProjectionCheckpointIndexMigrationsSchemaMigration"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterProjectionCheckpointIndexMigrationsSchemaMigration(IStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The stored name is the Orchard migration class's own name, so a database migrated under either
    /// host agrees on which version has already been applied.
    /// </remarks>
    public string Name => "ContactCenterProjectionCheckpointIndexMigrations";

    /// <summary>
    /// Creates the projection-checkpoint index table and its per-handler uniqueness constraint.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync(ISchemaBuilder builder)
    {
        await builder.CreateMapIndexTableAsync<ContactCenterProjectionCheckpointIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("HandlerId", column => column.NotNull().WithLength(128))
            .Column<int>("Version"),
            collection: ContactCenterStorage.CollectionName);

        await builder.AlterIndexTableAsync<ContactCenterProjectionCheckpointIndex>(table => table
            .CreateIndex(
                "IDX_ContactCenterProjectionCheckpointIndex_Handler",
                "HandlerId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        await ContactCenterMigrationSql.CreateUniqueIndexAsync(
            builder,
            _store,
            typeof(ContactCenterProjectionCheckpointIndex),
            "UQ_ContactCenterProjectionCheckpointIndex_Handler",
            "HandlerId");

        return 1;
    }

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    public Task<int> UpdateFromAsync(int version, ISchemaBuilder builder)
    {
        return Task.FromResult(version);
    }
}
