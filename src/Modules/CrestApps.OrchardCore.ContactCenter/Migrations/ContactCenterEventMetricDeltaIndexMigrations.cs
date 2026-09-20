using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterEventMetricDeltaIndex"/>.
/// </summary>
internal sealed class ContactCenterEventMetricDeltaIndexMigrations : DataMigration
{
    private readonly ContactCenterEventMetricDeltaIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEventMetricDeltaIndexMigrations"/> class.
    /// </summary>
    public ContactCenterEventMetricDeltaIndexMigrations()
    {
        _step = new ContactCenterEventMetricDeltaIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the event metric contribution index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
