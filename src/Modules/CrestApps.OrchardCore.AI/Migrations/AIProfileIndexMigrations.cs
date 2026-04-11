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

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }

    public static Task<int> UpdateFrom2Async()
    {
        return Task.FromResult(3);
    }
}
