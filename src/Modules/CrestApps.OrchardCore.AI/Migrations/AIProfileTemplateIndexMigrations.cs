using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIProfileTemplateIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIProfileTemplateIndexSchemaAsync(AIConstants.AICollectionName);

        return 2;
    }

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }
}
