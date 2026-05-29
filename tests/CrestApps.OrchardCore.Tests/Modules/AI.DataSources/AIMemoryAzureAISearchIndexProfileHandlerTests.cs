using System.Text.Json.Nodes;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using Moq;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class AIMemoryAzureAISearchIndexProfileHandlerTests
{
    [Fact]
    public async Task Handler_DeduplicatesManagedMappingsAndPreservesCustomMappings()
    {
        var handler = new CrestApps.OrchardCore.AI.Memory.AzureAI.Handlers.AIMemoryAzureAISearchIndexProfileHandler(
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIClientFactory>());
        var indexProfile = new IndexProfile
        {
            ProviderName = AzureAISearchConstants.ProviderName,
            Type = MemoryConstants.IndexingTaskType,
        };
        indexProfile.Put(new AzureAISearchIndexMetadata
        {
            IndexMappings =
            [
                new AzureAISearchIndexMap { AzureFieldKey = MemoryConstants.ColumnNames.Content, Type = DocumentIndex.Types.Text, IsSearchable = true },
                new AzureAISearchIndexMap { AzureFieldKey = MemoryConstants.ColumnNames.Content, Type = DocumentIndex.Types.Text, IsSearchable = true },
                new AzureAISearchIndexMap { AzureFieldKey = MemoryConstants.ColumnNames.Embedding, Type = DocumentIndex.Types.Vector, IsSearchable = true },
                new AzureAISearchIndexMap { AzureFieldKey = MemoryConstants.ColumnNames.Embedding, Type = DocumentIndex.Types.Vector, IsSearchable = true },
                new AzureAISearchIndexMap { AzureFieldKey = "CustomMemoryField", IndexingKey = "CustomMemoryField", Type = DocumentIndex.Types.Text, IsFilterable = true },
            ],
        });

        await handler.LoadedAsync(new LoadedContext<IndexProfile>(indexProfile));
        await handler.UpdatingAsync(new UpdatingContext<IndexProfile>(indexProfile, new JsonObject()));

        Assert.True(indexProfile.TryGet<AzureAISearchIndexMetadata>(out var metadata));
        Assert.Equal(8, metadata.IndexMappings.Count);
        Assert.Single(metadata.IndexMappings, map => string.Equals(map.AzureFieldKey, MemoryConstants.ColumnNames.Content, StringComparison.OrdinalIgnoreCase));
        Assert.Single(metadata.IndexMappings, map => string.Equals(map.AzureFieldKey, MemoryConstants.ColumnNames.Embedding, StringComparison.OrdinalIgnoreCase));
        Assert.Single(metadata.IndexMappings, map => string.Equals(map.AzureFieldKey, "CustomMemoryField", StringComparison.OrdinalIgnoreCase));
        Assert.All(metadata.IndexMappings.Where(map => string.Equals(map.AzureFieldKey, MemoryConstants.ColumnNames.Content, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(map.AzureFieldKey, MemoryConstants.ColumnNames.Embedding, StringComparison.OrdinalIgnoreCase)),
            map => Assert.Equal(map.AzureFieldKey, map.IndexingKey));
    }
}
