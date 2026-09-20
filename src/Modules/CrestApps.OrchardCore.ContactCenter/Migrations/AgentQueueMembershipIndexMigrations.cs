using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentQueueMembershipIndex"/>.
/// </summary>
internal sealed class AgentQueueMembershipIndexMigrations : DataMigration
{
    private readonly AgentQueueMembershipIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentQueueMembershipIndexMigrations"/> class.
    /// </summary>
    public AgentQueueMembershipIndexMigrations()
    {
        _step = new AgentQueueMembershipIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the agent queue membership index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
