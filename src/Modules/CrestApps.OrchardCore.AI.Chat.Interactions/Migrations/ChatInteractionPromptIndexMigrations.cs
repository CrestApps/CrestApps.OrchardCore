using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;

internal sealed class ChatInteractionPromptIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionPromptIndexMigrations"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    public ChatInteractionPromptIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateChatInteractionPromptIndexSchemaAsync(_option);

        return 1;
    }
}
