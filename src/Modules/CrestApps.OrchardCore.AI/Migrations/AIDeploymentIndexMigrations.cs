using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

public sealed class AIDeploymentIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIDeploymentIndexSchemaAsync(AIConstants.AICollectionName);

        return 1;
    }
}
