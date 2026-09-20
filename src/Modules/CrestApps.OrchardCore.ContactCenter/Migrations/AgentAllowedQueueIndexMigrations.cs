using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentAllowedQueueIndex"/>.
/// </summary>
internal sealed class AgentAllowedQueueIndexMigrations : DataMigration
{
    private readonly AgentAllowedQueueIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAllowedQueueIndexMigrations"/> class.
    /// </summary>
    public AgentAllowedQueueIndexMigrations()
    {
        _step = new AgentAllowedQueueIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the agent allowed-queue index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
