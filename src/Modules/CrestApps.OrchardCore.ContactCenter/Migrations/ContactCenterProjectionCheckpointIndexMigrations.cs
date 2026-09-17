using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterProjectionCheckpointIndex"/> and enforces a single
/// checkpoint per projection handler through a unique constraint.
/// </summary>
internal sealed class ContactCenterProjectionCheckpointIndexMigrations : DataMigration
{
    private readonly ContactCenterProjectionCheckpointIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProjectionCheckpointIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public ContactCenterProjectionCheckpointIndexMigrations(IStore store)
    {
        _step = new ContactCenterProjectionCheckpointIndexMigrationsSchemaMigration(store);
    }

    /// <summary>
    /// Creates the projection-checkpoint index table and its per-handler uniqueness constraint.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
