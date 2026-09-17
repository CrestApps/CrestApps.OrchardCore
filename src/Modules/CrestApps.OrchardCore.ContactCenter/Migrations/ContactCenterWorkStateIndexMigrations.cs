using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterWorkStateIndex"/>.
/// </summary>
internal sealed class ContactCenterWorkStateIndexMigrations : DataMigration
{
    private readonly ContactCenterWorkStateIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkStateIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterWorkStateIndexMigrations(
        IStore store,
        TimeProvider timeProvider)
    {
        _step = new ContactCenterWorkStateIndexMigrationsSchemaMigration(store, timeProvider);
    }

    /// <summary>
    /// Creates the work state index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the modification time the work state is purged by. Nothing in the product ever deletes a work
    /// state, so without an age this table grows by one row for every activity that is ever routed.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);
}
