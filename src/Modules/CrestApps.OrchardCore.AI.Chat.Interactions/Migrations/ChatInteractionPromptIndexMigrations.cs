using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;

internal sealed class ChatInteractionPromptIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public ChatInteractionPromptIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateChatInteractionPromptIndexSchemaAsync(_option);

        return 1;
    }
}
