using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class IndexProfileEmbeddingMetadataMigrationsTests
{
    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenLegacyAIMemoryMetadataExists_StoresCanonicalMetadata()
    {
        var indexProfile = new IndexProfile();
        indexProfile.Put("AIMemoryIndexProfileMetadata", new DataSourceIndexProfileMetadata
        {
            EmbeddingDeploymentName = "embedding-1",
        });

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, Mock.Of<IAIDeploymentManager>());

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("embedding-1", metadata.GetEmbeddingDeploymentName());
        Assert.False(indexProfile.Has("AIMemoryIndexProfileMetadata"));
    }

    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenLegacyChatInteractionMetadataExists_StoresCanonicalMetadata()
    {
        var indexProfile = new IndexProfile();
        indexProfile.Put("ChatInteractionIndexProfileMetadata", new DataSourceIndexProfileMetadata
        {
            EmbeddingDeploymentName = "embedding-2",
        });

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, Mock.Of<IAIDeploymentManager>());

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("embedding-2", metadata.GetEmbeddingDeploymentName());
        Assert.False(indexProfile.Has("ChatInteractionIndexProfileMetadata"));
    }

    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenOnlyLegacyProviderFieldsExist_ResolvesCanonicalDeploymentName()
    {
        var indexProfile = new IndexProfile();
        indexProfile.Put("AIMemoryIndexProfileMetadata", new JsonObject
        {
            ["EmbeddingProviderName"] = "AzureOpenAI",
            ["EmbeddingConnectionName"] = "Default",
            ["EmbeddingDeploymentName"] = "text-embedding-3-small",
        });

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByNameAsync("text-embedding-3-small"))
            .ReturnsAsync((AIDeployment)null);
        deploymentManager
            .Setup(manager => manager.GetByTypeAsync(AIDeploymentType.Embedding))
            .ReturnsAsync(
            [
                new AIDeployment
                {
                    ItemId = "embedding-3",
                    ClientName = "AzureOpenAI",
                    ConnectionName = "Default",
                    Name = "my-embedding-deployment",
                    ModelName = "text-embedding-3-small",
                    Type = AIDeploymentType.Embedding,
                },
            ]);
        deploymentManager
            .Setup(manager => manager.GetAllAsync("AzureOpenAI"))
            .ReturnsAsync(
            [
                new AIDeployment
                {
                    ItemId = "embedding-3",
                    ClientName = "AzureOpenAI",
                    ConnectionName = "Default",
                    Name = "my-embedding-deployment",
                    ModelName = "text-embedding-3-small",
                    Type = AIDeploymentType.Embedding,
                },
            ]);

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager.Object);

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("my-embedding-deployment", metadata.GetEmbeddingDeploymentName());
        Assert.False(indexProfile.Has("AIMemoryIndexProfileMetadata"));
    }

    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenCanonicalMetadataStoresModelName_RewritesToDeploymentName()
    {
        var indexProfile = new IndexProfile();
        indexProfile.Put(nameof(DataSourceIndexProfileMetadata), new JsonObject
        {
            ["EmbeddingProviderName"] = "AzureOpenAI",
            ["EmbeddingConnectionName"] = "Default",
            ["EmbeddingDeploymentName"] = "text-embedding-3-small",
        });

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByNameAsync("text-embedding-3-small"))
            .ReturnsAsync((AIDeployment)null);
        deploymentManager
            .Setup(manager => manager.GetByTypeAsync(AIDeploymentType.Embedding))
            .ReturnsAsync(
            [
                new AIDeployment
                {
                    ItemId = "embedding-4",
                    ClientName = "AzureOpenAI",
                    ConnectionName = "Default",
                    Name = "embedding-prod-eastus",
                    ModelName = "text-embedding-3-small",
                    Type = AIDeploymentType.Embedding,
                },
            ]);
        deploymentManager
            .Setup(manager => manager.GetAllAsync("AzureOpenAI"))
            .ReturnsAsync(
            [
                new AIDeployment
                {
                    ItemId = "embedding-4",
                    ClientName = "AzureOpenAI",
                    ConnectionName = "Default",
                    Name = "embedding-prod-eastus",
                    ModelName = "text-embedding-3-small",
                    Type = AIDeploymentType.Embedding,
                },
            ]);

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager.Object);

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("embedding-prod-eastus", metadata.GetEmbeddingDeploymentName());
    }

    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenCanonicalDeploymentNameAlreadyUsesTechnicalName_PreservesIt()
    {
        var indexProfile = new IndexProfile();
        indexProfile.Put(nameof(DataSourceIndexProfileMetadata), new JsonObject
        {
            ["EmbeddingProviderName"] = "AzureOpenAI",
            ["EmbeddingConnectionName"] = "Default",
            ["EmbeddingDeploymentName"] = "embedding-technical-name",
        });

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByNameAsync("embedding-technical-name"))
            .ReturnsAsync(new AIDeployment
            {
                ItemId = "embedding-5",
                ClientName = "AzureOpenAI",
                ConnectionName = "Default",
                Name = "embedding-technical-name",
                ModelName = "text-embedding-3-small",
                Type = AIDeploymentType.Embedding,
            });

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager.Object);

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("embedding-technical-name", metadata.GetEmbeddingDeploymentName());
    }

    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenLegacyEmbeddingDeploymentIdExists_UsesDeploymentNameFromId()
    {
        var indexProfile = new IndexProfile();
        indexProfile.Put(nameof(DataSourceIndexProfileMetadata), new JsonObject
        {
            ["EmbeddingDeploymentId"] = "deployment-id-123",
            ["EmbeddingDeploymentName"] = "text-embedding-3-small",
        });

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByIdAsync("deployment-id-123"))
            .ReturnsAsync(new AIDeployment
            {
                ItemId = "deployment-id-123",
                Name = "embedding-eastus-prod",
                ModelName = "text-embedding-3-small",
                Type = AIDeploymentType.Embedding,
            });

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager.Object);

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("embedding-eastus-prod", metadata.GetEmbeddingDeploymentName());
    }

    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenLegacyAzureAliasIsUsed_ResolvesCurrentAzureOpenAIProviderDeployment()
    {
        var indexProfile = new IndexProfile();
        indexProfile.Put(nameof(DataSourceIndexProfileMetadata), new JsonObject
        {
            ["EmbeddingProviderName"] = "Azure",
            ["EmbeddingConnectionName"] = "winnerware",
            ["EmbeddingDeploymentName"] = "text-embedding-3-small",
        });

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByNameAsync("text-embedding-3-small"))
            .ReturnsAsync((AIDeployment)null);
        deploymentManager
            .Setup(manager => manager.GetByTypeAsync(AIDeploymentType.Embedding))
            .ReturnsAsync(Array.Empty<AIDeployment>());
        deploymentManager
            .Setup(manager => manager.GetAllAsync("Azure"))
            .ReturnsAsync(Array.Empty<AIDeployment>());
        deploymentManager
            .Setup(manager => manager.GetAllAsync("AzureOpenAI"))
            .ReturnsAsync(
            [
                new AIDeployment
                {
                    ItemId = "embedding-6",
                    ClientName = "AzureOpenAI",
                    ConnectionName = "winnerware",
                    Name = "winnerware-embedding-eastus",
                    ModelName = "text-embedding-3-small",
                    Type = AIDeploymentType.Embedding,
                },
            ]);
        deploymentManager
            .Setup(manager => manager.GetAllAsync("AzureOpenAIOwnData"))
            .ReturnsAsync(Array.Empty<AIDeployment>());

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager.Object);

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("winnerware-embedding-eastus", metadata.GetEmbeddingDeploymentName());
    }

    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenLegacySelectorIncludesAzurePrefix_StripsPrefixBeforeMatching()
    {
        var indexProfile = new IndexProfile();
        indexProfile.Put(nameof(DataSourceIndexProfileMetadata), new JsonObject
        {
            ["EmbeddingProviderName"] = "Azure",
            ["EmbeddingConnectionName"] = "winnerware",
            ["EmbeddingDeploymentName"] = "Azure-text-embedding-3-small",
        });

        var deploymentManager = new Mock<IAIDeploymentManager>();
        deploymentManager
            .Setup(manager => manager.FindByNameAsync("Azure-text-embedding-3-small"))
            .ReturnsAsync((AIDeployment)null);
        deploymentManager
            .Setup(manager => manager.GetByTypeAsync(AIDeploymentType.Embedding))
            .ReturnsAsync(
            [
                new AIDeployment
                {
                    ItemId = "embedding-7",
                    ClientName = "AzureOpenAI",
                    ConnectionName = "winnerware",
                    Name = "winnerware-embedding-eastus",
                    ModelName = "text-embedding-3-small",
                    Type = AIDeploymentType.Embedding,
                },
            ]);

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager.Object);

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("winnerware-embedding-eastus", metadata.GetEmbeddingDeploymentName());
    }

    private static async Task<bool> InvokeNormalizeIndexProfileMetadataAsync(IndexProfile indexProfile, IAIDeploymentManager deploymentManager)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.IndexProfileEmbeddingMetadataMigrations", throwOnError: true)!
            .GetMethod("NormalizeIndexProfileMetadataAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

        var task = (Task<bool>)method.Invoke(null, [indexProfile, deploymentManager, NullLogger.Instance])!;

        return await task;
    }
}
