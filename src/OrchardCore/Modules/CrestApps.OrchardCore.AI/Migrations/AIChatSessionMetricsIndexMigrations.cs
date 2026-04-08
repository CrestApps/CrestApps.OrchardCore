using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class AIChatSessionMetricsIndexMigrations : DataMigration
{
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

        await SchemaBuilder.CreateAIChatSessionMetricsSchemaAsync(options);

        return 4;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AddAIChatSessionMetricsConversionColumnsAsync(AIConstants.AICollectionName);

        return 2;
    }

    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AddAIChatSessionMetricsThumbColumnsAsync(AIConstants.AICollectionName);

        return 3;
    }

    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AddAIChatSessionMetricsCompletionCountColumnAsync(AIConstants.AICollectionName);

        return 4;
    }
}
