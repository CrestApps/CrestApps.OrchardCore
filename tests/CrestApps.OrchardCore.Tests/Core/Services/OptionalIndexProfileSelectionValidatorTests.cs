using CrestApps.OrchardCore.AI.Core.Services;
using Moq;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class OptionalIndexProfileSelectionValidatorTests
{
    [Fact]
    public async Task IsValidAsync_WhenIndexProfileNameIsEmpty_ShouldReturnTrue()
    {
        var indexProfileStore = new Mock<IIndexProfileStore>();

        var result = await OptionalIndexProfileSelectionValidator.IsValidAsync(indexProfileStore.Object, null, "expected-type");

        Assert.True(result);
        indexProfileStore.Verify(store => store.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task IsValidAsync_WhenIndexProfileMatchesExpectedType_ShouldReturnTrue()
    {
        var indexProfileStore = new Mock<IIndexProfileStore>();
        indexProfileStore
            .Setup(store => store.FindByNameAsync("valid-profile"))
            .ReturnsAsync(new IndexProfile
            {
                Name = "valid-profile",
                Type = "expected-type",
            });

        var result = await OptionalIndexProfileSelectionValidator.IsValidAsync(indexProfileStore.Object, "valid-profile", "expected-type");

        Assert.True(result);
    }

    [Fact]
    public async Task IsValidAsync_WhenIndexProfileHasWrongType_ShouldReturnFalse()
    {
        var indexProfileStore = new Mock<IIndexProfileStore>();
        indexProfileStore
            .Setup(store => store.FindByNameAsync("wrong-profile"))
            .ReturnsAsync(new IndexProfile
            {
                Name = "wrong-profile",
                Type = "different-type",
            });

        var result = await OptionalIndexProfileSelectionValidator.IsValidAsync(indexProfileStore.Object, "wrong-profile", "expected-type");

        Assert.False(result);
    }

    [Fact]
    public async Task IsValidAsync_WhenIndexProfileDoesNotExist_ShouldReturnFalse()
    {
        var indexProfileStore = new Mock<IIndexProfileStore>();
        indexProfileStore
            .Setup(store => store.FindByNameAsync("missing-profile"))
            .ReturnsAsync((IndexProfile)null);

        var result = await OptionalIndexProfileSelectionValidator.IsValidAsync(indexProfileStore.Object, "missing-profile", "expected-type");

        Assert.False(result);
    }
}
