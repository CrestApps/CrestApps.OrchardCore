using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ServiceLevels.Migrations;

/// <summary>
/// Creates the index tables of service level policies and timers.
/// </summary>
internal sealed class ServiceLevelsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await ServiceLevelsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
