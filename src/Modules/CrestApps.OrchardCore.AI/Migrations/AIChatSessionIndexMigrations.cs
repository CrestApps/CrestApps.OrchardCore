using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql.Sql;

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

        return 4;
    }

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(4);
    }

    public static Task<int> UpdateFrom2Async()
    {
        return Task.FromResult(4);
    }

    public async Task<int> UpdateFrom3Async()
    {
        try
        {
            await SchemaBuilder.AlterIndexTableAsync<AIChatSessionIndex>(table =>
            {
                table.AddColumn<ChatSessionStatus>("Status");
            }, collection: _option.AICollectionName);
        }
        catch
        {
        }

        return 4;
    }
}
