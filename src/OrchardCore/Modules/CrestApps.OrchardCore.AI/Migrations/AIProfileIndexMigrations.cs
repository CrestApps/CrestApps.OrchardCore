using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIProfileIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIProfileIndexSchemaAsync(AIConstants.AICollectionName);

        return 3;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AddAIProfileIndexDescriptionColumnAsync(AIConstants.AICollectionName);

        return 2;
    }

    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AddAIProfileIndexDeploymentNameColumnAsync(AIConstants.AICollectionName);

        return 3;
    }
}
