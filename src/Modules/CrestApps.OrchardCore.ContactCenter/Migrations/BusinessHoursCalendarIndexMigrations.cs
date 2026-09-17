using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="BusinessHoursCalendarIndex"/>.
/// </summary>
internal sealed class BusinessHoursCalendarIndexMigrations : DataMigration
{
    private readonly BusinessHoursCalendarIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarIndexMigrations"/> class.
    /// </summary>
    public BusinessHoursCalendarIndexMigrations()
    {
        _step = new BusinessHoursCalendarIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the business-hours calendar index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
