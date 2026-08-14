using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class DataSourceAzureAISearchIndexProfileHandlerTests
{
    [Fact]
    public async Task InitializingAsync_SetsStableIndexingKeysWithoutDuplicatingMappings()
    {
        var (handlerType, handler) = CreateHandler();
        var method = handlerType.GetMethod("InitializingAsync")!;
        var indexProfile = new IndexProfile
        {
            ProviderName = AzureAISearchConstants.ProviderName,
            Type = DataSourceConstants.IndexingTaskType,
        };
        var context = new InitializingContext<IndexProfile>(indexProfile, new JsonObject());

        await (Task)method.Invoke(handler, [context])!;
        await (Task)method.Invoke(handler, [context])!;

        Assert.True(indexProfile.TryGet<AzureAISearchIndexMetadata>(out var metadata));
        Assert.Equal(9, metadata.IndexMappings.Count);
        Assert.All(metadata.IndexMappings, map =>
        {
            Assert.False(string.IsNullOrWhiteSpace(map.IndexingKey));
            Assert.Equal(map.AzureFieldKey, map.IndexingKey);
        });
    }

    [Fact]
    public async Task LoadedAsync_BackfillsMissingIndexingKeysFromAzureFieldKeys()
    {
        var (handlerType, handler) = CreateHandler();
        var method = handlerType.GetMethod("LoadedAsync")!;
        var indexProfile = new IndexProfile
        {
            ProviderName = AzureAISearchConstants.ProviderName,
            Type = DataSourceConstants.IndexingTaskType,
        };
        indexProfile.Put(new AzureAISearchIndexMetadata
        {
            IndexMappings =
            [
                new AzureAISearchIndexMap
                {
                    AzureFieldKey = DataSourceConstants.ColumnNames.ChunkId,
                    Type = DocumentIndex.Types.Text,
                    IsKey = true,
                    IsFilterable = true,
                },
                new AzureAISearchIndexMap
                {
                    AzureFieldKey = DataSourceConstants.ColumnNames.Content,
                    Type = DocumentIndex.Types.Text,
                    IsSearchable = true,
                },
            ],
        });
        var context = new LoadedContext<IndexProfile>(indexProfile);

        await (Task)method.Invoke(handler, [context])!;

        Assert.True(indexProfile.TryGet<AzureAISearchIndexMetadata>(out var metadata));
        Assert.Collection(metadata.IndexMappings,
            map =>
            {
                Assert.Equal(DataSourceConstants.ColumnNames.ChunkId, map.IndexingKey);
                Assert.True(map.IsKey);
                Assert.True(map.IsFilterable);
            },
            map =>
            {
                Assert.Equal(DataSourceConstants.ColumnNames.Content, map.IndexingKey);
                Assert.True(map.IsSearchable);
            });
    }

    private static (Type HandlerType, object Handler) CreateHandler()
    {
        var handlerType = typeof(CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers.DataSourceAzureAISearchIndexProfileHandler", throwOnError: true)!;
        var handler = Activator.CreateInstance(
            handlerType,
            Mock.Of<IAIDeploymentManager>(),
            Mock.Of<IAIClientFactory>(),
            GetNullLogger(handlerType))!;

        return (handlerType, handler);
    }

    private static object GetNullLogger(Type handlerType)
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var method = typeof(LoggerFactoryExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == nameof(LoggerFactoryExtensions.CreateLogger) &&
                candidate.IsGenericMethodDefinition &&
                candidate.GetParameters().Length == 1);

        return method.MakeGenericMethod(handlerType).Invoke(null, [loggerFactory])!;
    }
}
