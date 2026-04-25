using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;
    private readonly ILogger<AIChatSessionIndexMigrations> _logger;

    public AIChatSessionIndexMigrations(
        IOptions<YesSqlStoreOptions> option,
        ILogger<AIChatSessionIndexMigrations> logger)
    {
        _option = option.Value;
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add the Status column to the AI chat session index table.");
            throw;
        }

        return 4;
    }
}
