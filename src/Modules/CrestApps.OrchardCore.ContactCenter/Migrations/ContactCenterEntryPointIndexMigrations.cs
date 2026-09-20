using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterEntryPointIndex"/>.
/// </summary>
internal sealed class ContactCenterEntryPointIndexMigrations : DataMigration
{
    private readonly ContactCenterEntryPointIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointIndexMigrations"/> class.
    /// </summary>
    public ContactCenterEntryPointIndexMigrations()
    {
        _step = new ContactCenterEntryPointIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the entry point index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
