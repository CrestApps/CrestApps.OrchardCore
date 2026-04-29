using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
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
            .ReturnsAsync(new AIDeployment
            {
                ItemId = "embedding-3",
                ClientName = "AzureOpenAI",
                ConnectionName = "Default",
                Name = "text-embedding-3-small",
                ModelName = "text-embedding-3-small",
                Type = AIDeploymentType.Embedding,
            });

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager.Object);

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("text-embedding-3-small", metadata.GetEmbeddingDeploymentName());
        Assert.False(indexProfile.Has("AIMemoryIndexProfileMetadata"));
    }

    [Fact]
    public async Task NormalizeIndexProfileMetadataAsync_WhenCanonicalLegacyProviderFieldsExist_ResolvesCanonicalDeploymentName()
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
            .ReturnsAsync(new AIDeployment
            {
                ItemId = "embedding-4",
                ClientName = "AzureOpenAI",
                ConnectionName = "Default",
                Name = "text-embedding-3-small",
                ModelName = "text-embedding-3-small",
                Type = AIDeploymentType.Embedding,
            });

        var updated = await InvokeNormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager.Object);

        Assert.True(updated);
        Assert.True(indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata));
        Assert.Equal("text-embedding-3-small", metadata.GetEmbeddingDeploymentName());
    }

    private static async Task<bool> InvokeNormalizeIndexProfileMetadataAsync(IndexProfile indexProfile, IAIDeploymentManager deploymentManager)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.IndexProfileEmbeddingMetadataMigrations", throwOnError: true)!
            .GetMethod("NormalizeIndexProfileMetadataAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

        var task = (Task<bool>)method.Invoke(null, [indexProfile, deploymentManager])!;

        return await task;
    }
}
