using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Chat.Migrations;

internal sealed class AIChatSessionExtractedDataMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIChatSessionExtractedDataMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIChatSessionExtractedDataIndexSchemaAsync(_option);

        return 1;
    }
}
