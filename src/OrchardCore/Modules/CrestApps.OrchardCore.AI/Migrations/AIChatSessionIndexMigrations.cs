using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionIndexMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIChatSessionIndexSchemaAsync(AIConstants.AICollectionName);

        return 3;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AddAIChatSessionIndexStatusColumnsAsync(AIConstants.AICollectionName);

        return 3;
    }

    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AddAIChatSessionIndexPostSessionProcessingStatusColumnAsync(AIConstants.AICollectionName);

        return 3;
    }
}
