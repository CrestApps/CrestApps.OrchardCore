using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="ContactCenterSkillIndex"/>.
/// </summary>
internal sealed class ContactCenterSkillIndexMigrations : DataMigration
{
    private readonly ContactCenterSkillIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillIndexMigrations"/> class.
    /// </summary>
    public ContactCenterSkillIndexMigrations()
    {
        _step = new ContactCenterSkillIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the skill index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
