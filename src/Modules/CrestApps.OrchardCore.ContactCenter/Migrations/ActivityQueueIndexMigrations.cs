using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ActivityQueueIndex"/>.
/// </summary>
internal sealed class ActivityQueueIndexMigrations : DataMigration
{
    private readonly ActivityQueueIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueIndexMigrations"/> class.
    /// </summary>
    public ActivityQueueIndexMigrations()
    {
        _step = new ActivityQueueIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the queue index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the optional queue-group identifier used by catalog organization and reporting.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);
}
