using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionMetricsIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _option;

    public AIChatSessionMetricsIndexMigrations(IOptions<YesSqlStoreOptions> option)
    {
        _option = option.Value;
    }

    public async Task<int> CreateAsync()
    {
        var options = new AIChatSessionMetricsIndexSchemaOptions
        {
            CollectionName = AIConstants.AICollectionName,
            SessionIdLength = 26,
            ProfileIdLength = 26,
            VisitorIdLength = 64,
            UserIdLength = 26,
            CreateNamedIndexes = true,
        };

        await SchemaBuilder.CreateAIChatSessionMetricsSchemaAsync(_option, options);

        return 4;
    }

    public static Task<int> UpdateFrom1Async()
    {
        return Task.FromResult(2);
    }

    public static Task<int> UpdateFrom2Async()
    {
        return Task.FromResult(3);
    }

    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AddAIChatSessionMetricsCompletionCountColumnAsync(_option);

        return 4;
    }
}
