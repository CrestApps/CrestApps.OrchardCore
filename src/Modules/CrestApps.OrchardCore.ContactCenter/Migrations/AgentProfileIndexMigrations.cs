using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentProfileIndex"/>.
/// </summary>
internal sealed class AgentProfileIndexMigrations : DataMigration
{
    private readonly AgentProfileIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProfileIndexMigrations"/> class.
    /// </summary>
    public AgentProfileIndexMigrations()
    {
        _step = new AgentProfileIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the agent profile index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
