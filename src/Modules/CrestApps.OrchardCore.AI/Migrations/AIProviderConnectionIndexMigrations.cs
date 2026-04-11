using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

public sealed class AIProviderConnectionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIProviderConnectionIndexSchemaAsync(AIConstants.AICollectionName);

        return 1;
    }
}
