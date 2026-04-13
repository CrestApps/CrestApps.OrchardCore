using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionPromptIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIChatSessionPromptIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIChatSessionPromptIndexSchemaAsync(_option);

        return 1;
    }
}
