using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIChatSessionIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateAIChatSessionIndexSchemaAsync(_option);

        return 3;
    }

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(3);
    }

    public static Task<int> UpdateFrom2Async()
    {
        return Task.FromResult(3);
    }
}
