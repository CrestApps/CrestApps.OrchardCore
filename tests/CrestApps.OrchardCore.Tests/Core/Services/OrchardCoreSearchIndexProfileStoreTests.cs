using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Moq;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class OrchardCoreSearchIndexProfileStoreTests
{
    [Fact]
    public async Task FindByNameAsync_WhenEmbeddingDeploymentExistsInMetadata_PopulatesMappedProfile()
    {
        var orchardStore = new Mock<IIndexProfileStore>();
        var orchardProfile = new IndexProfile
        {
            Id = "profile-1",
            Name = "memory-profile",
            IndexName = "memory-index",
            ProviderName = "AzureAISearch",
            IndexFullName = "tenant-memory-index",
            Type = "AIMemory",
        };

        IndexProfileEmbeddingMetadataAccessor.StoreMetadata(orchardProfile, new DataSourceIndexProfileMetadata
        {
            EmbeddingDeploymentName = "deployment-1",
        });

        orchardStore
            .Setup(store => store.FindByNameAsync("memory-profile"))
            .ReturnsAsync(orchardProfile);

        var store = new OrchardCoreSearchIndexProfileStore(orchardStore.Object);

        var mappedProfile = await store.FindByNameAsync("memory-profile", TestContext.Current.CancellationToken);

        Assert.NotNull(mappedProfile);
        Assert.Equal("deployment-1", mappedProfile.GetEmbeddingDeploymentName());
    }

    [Fact]
    public async Task UpdateAsync_WhenEmbeddingDeploymentIsSet_PersistsCanonicalMetadata()
    {
        var orchardStore = new Mock<IIndexProfileStore>();
        IndexProfile capturedProfile = null;
        orchardStore
            .Setup(store => store.UpdateAsync(It.IsAny<IndexProfile>()))
            .Callback<IndexProfile>(profile => capturedProfile = profile)
            .Returns(ValueTask.CompletedTask);

        var store = new OrchardCoreSearchIndexProfileStore(orchardStore.Object);

        await store.UpdateAsync(new SearchIndexProfile
        {
            ItemId = "profile-1",
            Name = "memory-profile",
            IndexName = "memory-index",
            ProviderName = "AzureAISearch",
            IndexFullName = "tenant-memory-index",
            Type = "AIMemory",
            EmbeddingDeploymentName = "deployment-1",
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(capturedProfile);
        Assert.Equal("deployment-1", IndexProfileEmbeddingMetadataAccessor.GetMetadata(capturedProfile).GetEmbeddingDeploymentName());
    }
}
