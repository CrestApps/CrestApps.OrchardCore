using CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;

internal sealed class ChatInteractionMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateChatInteractionIndexSchemaAsync(AIConstants.AICollectionName);

        return 1;
    }
}
