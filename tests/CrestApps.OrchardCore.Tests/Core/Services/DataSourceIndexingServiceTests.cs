using System.Reflection;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class DataSourceIndexingServiceTests
{
    [Fact]
    public async Task ResolveEmbeddingDeploymentAsync_WhenSelectedProfileNeedsReload_UsesEmbeddingFromStoredIndexProfile()
    {
        // Arrange
        var indexProfileManager = new Mock<IIndexProfileManager>();
        var deploymentManager = new Mock<IAIDeploymentManager>();

        var listProfile = new IndexProfile
        {
            Id = "profile-1",
            Name = "knowledgebaseindex-jt-7",
            IndexName = "knowledgebaseindex-jt-7",
            ProviderName = "Elasticsearch",
            Type = "AIDataSource",
        };

        var storedProfile = new IndexProfile
        {
            Id = "profile-1",
            Name = "knowledgebaseindex-jt-7",
            IndexName = "knowledgebaseindex-jt-7",
            ProviderName = "Elasticsearch",
            Type = "AIDataSource",
        };

        IndexProfileEmbeddingMetadataAccessor.StoreMetadata(storedProfile, new DataSourceIndexProfileMetadata
        {
            EmbeddingDeploymentName = "text-embedding-3-small",
        });

        var deployment = new AIDeployment
        {
            ItemId = "deployment-1",
            Name = "text-embedding-3-small",
            Purpose = AIDeploymentPurpose.Embedding,
        };

        indexProfileManager
            .Setup(manager => manager.FindByIdAsync(listProfile.Id))
            .ReturnsAsync(storedProfile);

        deploymentManager
            .Setup(manager => manager.FindByNameAsync("text-embedding-3-small", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);

        var service = new DataSourceIndexingService(
            indexProfileManager.Object,
            Mock.Of<IAIDataSourceStore>(),
            deploymentManager.Object,
            Mock.Of<IAIClientFactory>(),
            Mock.Of<IAITextNormalizer>(),
            [],
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IDistributedLock>(),
            Mock.Of<IClock>(),
            NullLogger<DataSourceIndexingService>.Instance);

        // Act
        var resolvedDeployment = await InvokeResolveEmbeddingDeploymentAsync(service, listProfile);

        // Assert
        Assert.NotNull(resolvedDeployment);
        Assert.Equal("text-embedding-3-small", resolvedDeployment.Name);
    }

    [Fact]
    public async Task ResolveEmbeddingDeploymentAsync_WhenStoredSelectorUsesModelName_ResolvesAndPersistsTechnicalDeploymentName()
    {
        // Arrange
        var indexProfileManager = new Mock<IIndexProfileManager>();
        var deploymentManager = new Mock<IAIDeploymentManager>();

        var storedProfile = new IndexProfile
        {
            Id = "profile-1",
            Name = "knowledgebaseindex-jt-7",
            IndexName = "knowledgebaseindex-jt-7",
            ProviderName = "Elasticsearch",
            Type = "AIDataSource",
        };

        IndexProfileEmbeddingMetadataAccessor.StoreMetadata(storedProfile, new DataSourceIndexProfileMetadata
        {
            EmbeddingDeploymentName = "text-embedding-3-small",
        });

        var deployment = new AIDeployment
        {
            ItemId = "deployment-1",
            Name = "Azure-text-embedding-3-small",
            ModelName = "text-embedding-3-small",
            Purpose = AIDeploymentPurpose.Embedding,
        };

        indexProfileManager
            .Setup(manager => manager.FindByIdAsync(storedProfile.Id))
            .ReturnsAsync(storedProfile);
        indexProfileManager
            .Setup(manager => manager.UpdateAsync(storedProfile))
            .Returns(ValueTask.CompletedTask);

        deploymentManager
            .Setup(manager => manager.FindByNameAsync("text-embedding-3-small", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        deploymentManager
            .Setup(manager => manager.GetByPurposeAsync(AIDeploymentPurpose.Embedding, It.IsAny<CancellationToken>()))
            .ReturnsAsync([deployment]);

        var service = new DataSourceIndexingService(
            indexProfileManager.Object,
            Mock.Of<IAIDataSourceStore>(),
            deploymentManager.Object,
            Mock.Of<IAIClientFactory>(),
            Mock.Of<IAITextNormalizer>(),
            [],
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IDistributedLock>(),
            Mock.Of<IClock>(),
            NullLogger<DataSourceIndexingService>.Instance);

        // Act
        var resolvedDeployment = await InvokeResolveEmbeddingDeploymentAsync(service, storedProfile);

        // Assert
        Assert.NotNull(resolvedDeployment);
        Assert.Equal("Azure-text-embedding-3-small", resolvedDeployment.Name);
        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(storedProfile);
        Assert.Equal("Azure-text-embedding-3-small", metadata.GetEmbeddingDeploymentName());
        indexProfileManager.Verify(manager => manager.UpdateAsync(storedProfile), Times.Once);
    }

    private static async Task<AIDeployment> InvokeResolveEmbeddingDeploymentAsync(
        DataSourceIndexingService service,
        IndexProfile masterProfile)
    {
        var method = typeof(DataSourceIndexingService).GetMethod(
            "ResolveEmbeddingDeploymentAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = method.Invoke(service, [masterProfile, CancellationToken.None]) as Task<AIDeployment>;

        Assert.NotNull(task);

        return await task;
    }
}
