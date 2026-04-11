using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

internal sealed class AIChatSessionExtractedDataMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIChatSessionExtractedDataIndexSchemaAsync(AIConstants.AICollectionName);

        return 1;
    }
}
