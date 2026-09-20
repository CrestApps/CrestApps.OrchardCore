using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ActivityQueueGroupIndex"/>.
/// </summary>
internal sealed class ActivityQueueGroupIndexMigrations : DataMigration
{
    private readonly ActivityQueueGroupIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueGroupIndexMigrations"/> class.
    /// </summary>
    public ActivityQueueGroupIndexMigrations()
    {
        _step = new ActivityQueueGroupIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the queue-group index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
