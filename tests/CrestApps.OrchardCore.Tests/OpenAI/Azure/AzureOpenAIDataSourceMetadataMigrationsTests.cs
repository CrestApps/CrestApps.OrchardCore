using System.Reflection;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Tests.OpenAI.Azure;

public sealed class AzureOpenAIDataSourceMetadataMigrationsTests
{
    [Fact]
    public async Task MigrateKnowledgeBaseIndexesAsync_WhenLegacyDataSourcesExist_ShouldCreateOneWarehousePerProviderAndLinkDataSources()
    {
        // Arrange
        var elasticDs1 = new AIDataSource
        {
            ItemId = "ds-es-1",
            SourceIndexProfileName = "Contents-ES",
            Properties = new System.Text.Json.Nodes.JsonObject { ["ProviderName"] = "Elasticsearch" },
        };

        var elasticDs2 = new AIDataSource
        {
            ItemId = "ds-es-2",
            SourceIndexProfileName = "Contents-ES",
            Properties = new System.Text.Json.Nodes.JsonObject { ["ProviderName"] = "Elasticsearch" },
        };

        var azureDs1 = new AIDataSource
        {
            ItemId = "ds-az-1",
            SourceIndexProfileName = "Contents-AZ",
            Properties = new System.Text.Json.Nodes.JsonObject { ["ProviderName"] = "AzureAISearch" },
        };

        var dataSources = new List<AIDataSource> { elasticDs1, elasticDs2, azureDs1 };

        var dataSourceStore = new Mock<ICatalog<AIDataSource>>();
        dataSourceStore.Setup(x => x.GetAllAsync()).ReturnsAsync(dataSources);
        dataSourceStore.Setup(x => x.UpdateAsync(It.IsAny<AIDataSource>())).Returns(ValueTask.CompletedTask);

        var profiles = new List<IndexProfile>
        {
            new() { Name = "Contents-ES", IndexName = "Contents-ES", Type = IndexingConstants.ContentsIndexSource, ProviderName = "Elasticsearch" },
            new() { Name = "Contents-AZ", IndexName = "Contents-AZ", Type = IndexingConstants.ContentsIndexSource, ProviderName = "AzureAISearch" },
        };

        var indexProfileStore = new Mock<IIndexProfileStore>();
        indexProfileStore.Setup(x => x.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync((string type) =>
            profiles.Where(p => string.Equals(p.Type, type, StringComparison.OrdinalIgnoreCase)).ToList());
        indexProfileStore.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((string name) =>
            profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));

        var indexProfileManager = new Mock<IIndexProfileManager>();
        indexProfileManager.Setup(x => x.NewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Text.Json.Nodes.JsonNode>()))
            .ReturnsAsync((string provider, string type, System.Text.Json.Nodes.JsonNode _) =>
                new IndexProfile { ProviderName = provider, Type = type });

        indexProfileManager.Setup(x => x.CreateAsync(It.IsAny<IndexProfile>())).Returns<IndexProfile>(p =>
        {
            profiles.Add(p);
            return ValueTask.CompletedTask;
        });

        indexProfileManager.Setup(x => x.SynchronizeAsync(It.IsAny<IndexProfile>())).Returns(ValueTask.CompletedTask);

        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        // Act
        await InvokeMigrateKnowledgeBaseIndexesAsync(dataSourceStore.Object, indexProfileStore.Object, indexProfileManager.Object, serviceProvider);

        // Assert: created 1 warehouse index per provider
        var warehouses = profiles.Where(p => string.Equals(p.Type, DataSourceConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, warehouses.Count);
        Assert.Contains(warehouses, p => p.Name == "AIKnowledgeBaseWarehouse.Elasticsearch");
        Assert.Contains(warehouses, p => p.Name == "AIKnowledgeBaseWarehouse.AzureAISearch");

        // Assert: linked data sources
        Assert.Equal("AIKnowledgeBaseWarehouse.Elasticsearch", elasticDs1.AIKnowledgeBaseIndexProfileName);
        Assert.Equal("AIKnowledgeBaseWarehouse.Elasticsearch", elasticDs2.AIKnowledgeBaseIndexProfileName);
        Assert.Equal("AIKnowledgeBaseWarehouse.AzureAISearch", azureDs1.AIKnowledgeBaseIndexProfileName);

        // Assert: default field mappings applied
        Assert.Equal("Content.ContentItem.DisplayText.Analyzed", elasticDs1.TitleFieldName);
        Assert.Equal("Content.ContentItem.FullText", elasticDs1.ContentFieldName);
        Assert.Equal("Content__ContentItem__DisplayText__Analyzed", azureDs1.TitleFieldName);
        Assert.Equal("Content__ContentItem__FullText", azureDs1.ContentFieldName);

        // Assert: content index key field defaulted
        Assert.Equal("ContentItemId", elasticDs1.KeyFieldName);
        Assert.Equal("ContentItemId", azureDs1.KeyFieldName);
    }

    private static async Task InvokeMigrateKnowledgeBaseIndexesAsync(
        ICatalog<AIDataSource> dataSourceStore,
        IIndexProfileStore indexProfileStore,
        IIndexProfileManager indexProfileManager,
        IServiceProvider serviceProvider)
    {
        var assembly = Assembly.Load("CrestApps.OrchardCore.OpenAI.Azure");
        var type = assembly.GetType("CrestApps.OrchardCore.OpenAI.Azure.Migrations.AzureOpenAIDataSourceMetadataMigrations", throwOnError: true)!;
        var method = type.GetMethod("MigrateKnowledgeBaseIndexesAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

        var task = (Task)method.Invoke(null, [dataSourceStore, indexProfileStore, indexProfileManager, serviceProvider])!;
        await task;
    }
}
