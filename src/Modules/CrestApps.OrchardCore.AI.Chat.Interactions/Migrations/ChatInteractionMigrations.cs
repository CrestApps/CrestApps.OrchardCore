using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;

internal sealed class ChatInteractionMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public ChatInteractionMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateChatInteractionIndexSchemaAsync(_option);

        return 1;
    }
}
