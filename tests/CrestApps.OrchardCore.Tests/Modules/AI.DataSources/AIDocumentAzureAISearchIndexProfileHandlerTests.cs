using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class AIDocumentAzureAISearchIndexProfileHandlerTests
{
    [Fact]
    public async Task Handler_DeduplicatesManagedMappingsAndPreservesCustomMappings()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var handler = new CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers.AIDocumentAzureAISearchIndexProfileHandler(
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIClientFactory>(),
            loggerFactory.CreateLogger<CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers.AIDocumentAzureAISearchIndexProfileHandler>());
        var indexProfile = new IndexProfile
        {
            ProviderName = AzureAISearchConstants.ProviderName,
            Type = AIConstants.AIDocumentsIndexingTaskType,
        };
        indexProfile.Put(new AzureAISearchIndexMetadata
        {
            IndexMappings =
            [
                new AzureAISearchIndexMap { AzureFieldKey = AIConstants.ColumnNames.Content, Type = DocumentIndex.Types.Text, IsSearchable = true },
                new AzureAISearchIndexMap { AzureFieldKey = AIConstants.ColumnNames.Content, Type = DocumentIndex.Types.Text, IsSearchable = true },
                new AzureAISearchIndexMap { AzureFieldKey = AIConstants.ColumnNames.Embedding, Type = DocumentIndex.Types.Vector, IsSearchable = true },
                new AzureAISearchIndexMap { AzureFieldKey = AIConstants.ColumnNames.Embedding, Type = DocumentIndex.Types.Vector, IsSearchable = true },
                new AzureAISearchIndexMap { AzureFieldKey = "CustomField", IndexingKey = "CustomField", Type = DocumentIndex.Types.Text, IsFilterable = true },
            ],
        });

        await handler.LoadedAsync(new LoadedContext<IndexProfile>(indexProfile));
        await handler.UpdatingAsync(new UpdatingContext<IndexProfile>(indexProfile, new JsonObject()));

        Assert.True(indexProfile.TryGet<AzureAISearchIndexMetadata>(out var metadata));
        Assert.Equal(9, metadata.IndexMappings.Count);
        Assert.Single(metadata.IndexMappings, map => string.Equals(map.AzureFieldKey, AIConstants.ColumnNames.Content, StringComparison.OrdinalIgnoreCase));
        Assert.Single(metadata.IndexMappings, map => string.Equals(map.AzureFieldKey, AIConstants.ColumnNames.Embedding, StringComparison.OrdinalIgnoreCase));
        Assert.Single(metadata.IndexMappings, map => string.Equals(map.AzureFieldKey, "CustomField", StringComparison.OrdinalIgnoreCase));
        Assert.All(metadata.IndexMappings.Where(map => string.Equals(map.AzureFieldKey, AIConstants.ColumnNames.Content, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(map.AzureFieldKey, AIConstants.ColumnNames.Embedding, StringComparison.OrdinalIgnoreCase)),
            map => Assert.Equal(map.AzureFieldKey, map.IndexingKey));
    }
}
