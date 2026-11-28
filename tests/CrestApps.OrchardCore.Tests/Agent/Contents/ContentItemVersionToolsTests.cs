using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Agent.Contents;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

/// <summary>
/// The tools that list the versions of a content item, restore one as a draft, and discard a draft.
/// </summary>
public sealed class ContentItemVersionToolsTests
{
    [Fact]
    public async Task ListContentItemVersions_ReturnsEachVersionWithItsFlagsAndChecksViewOnTheLatest()
    {
        // Arrange
        var published = CreateVersion("version-1", latest: false, published: true);
        var draft = CreateVersion("version-2", latest: true, published: false);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAllVersionsAsync("item-1")).ReturnsAsync([published, draft]);
        var services = CreateServices(contentManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListContentItemVersionsTool(), services, new()
        {
            ["contentItemId"] = "item-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(2, items.Count);
        Assert.Contains(items, item => item.GetProperty("contentItemVersionId").GetString() == "version-1" && item.GetProperty("published").GetBoolean());
        Assert.Contains(items, item => item.GetProperty("contentItemVersionId").GetString() == "version-2" && item.GetProperty("latest").GetBoolean());

        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("ViewContent", check.Permission);
        Assert.Same(draft, check.Resource);
    }

    [Fact]
    public async Task ListContentItemVersions_WhenTheItemHasNoVersion_SaysItWasNotFound()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAllVersionsAsync("missing")).ReturnsAsync([]);
        var services = CreateServices(contentManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListContentItemVersionsTool(), services, new()
        {
            ["contentItemId"] = "missing",
        });

        // Assert
        Assert.Contains("No content item with the ID 'missing' was found", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreContentItemVersion_RestoresACopySoTheStoredVersionStaysInTheHistory()
    {
        // Arrange
        var version = CreateVersion("version-1", latest: false, published: true);
        ContentItem restored = null;
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetVersionAsync("version-1")).ReturnsAsync(version);
        contentManager.Setup(manager => manager.LoadAsync(It.IsAny<ContentItem>())).ReturnsAsync((ContentItem contentItem) => contentItem);
        contentManager
            .Setup(manager => manager.RestoreAsync(It.IsAny<ContentItem>()))
            .ReturnsAsync((ContentItem contentItem) =>
            {
                restored = contentItem;
                contentItem.ContentItemVersionId = "version-3";

                return new ContentValidateResult();
            });
        var services = CreateServices(contentManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RestoreContentItemVersionTool(), services, new()
        {
            ["contentItemVersionId"] = "version-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("restored").GetBoolean());
        Assert.Equal("item-1", json.GetProperty("contentItemId").GetString());
        Assert.Equal("version-1", json.GetProperty("restoredFromVersionId").GetString());
        Assert.Equal("version-3", json.GetProperty("draftVersionId").GetString());

        // The restore turns the item it is given into the new draft; the stored version must not be that item.
        Assert.NotNull(restored);
        Assert.NotSame(version, restored);
        Assert.Equal("version-1", version.ContentItemVersionId);
        Assert.True(version.Published);

        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("PublishContent", check.Permission);
    }

    [Fact]
    public async Task RestoreContentItemVersion_WhenTheVersionIsNotValid_ReportsTheErrors()
    {
        // Arrange
        var version = CreateVersion("version-1", latest: false, published: false);
        var failure = new ContentValidateResult();
        failure.Fail(new ValidationResult("The title is required."));

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetVersionAsync("version-1")).ReturnsAsync(version);
        contentManager.Setup(manager => manager.LoadAsync(It.IsAny<ContentItem>())).ReturnsAsync((ContentItem contentItem) => contentItem);
        contentManager.Setup(manager => manager.RestoreAsync(It.IsAny<ContentItem>())).ReturnsAsync(failure);
        var services = CreateServices(contentManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RestoreContentItemVersionTool(), services, new()
        {
            ["contentItemVersionId"] = "version-1",
        });

        // Assert
        Assert.StartsWith("Nothing was restored", result, StringComparison.Ordinal);
        Assert.Contains("The title is required.", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing", "No content item version with the ID 'missing' was found")]
    [InlineData("version-2", "already the latest version")]
    public async Task RestoreContentItemVersion_WhenThereIsNothingToRestore_SaysWhyAndRestoresNothing(string versionId, string expected)
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetVersionAsync("version-2")).ReturnsAsync(CreateVersion("version-2", latest: true, published: false));
        var services = CreateServices(contentManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RestoreContentItemVersionTool(), services, new()
        {
            ["contentItemVersionId"] = versionId,
        });

        // Assert
        Assert.Contains(expected, result, StringComparison.Ordinal);
        contentManager.Verify(manager => manager.RestoreAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task DiscardContentItemDraft_DiscardsTheDraftAndSaysThePublishedVersionIsBack()
    {
        // Arrange
        var draft = CreateVersion("version-2", latest: true, published: false);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("item-1", VersionOptions.Latest)).ReturnsAsync(draft);
        contentManager.Setup(manager => manager.GetAsync("item-1", VersionOptions.Published)).ReturnsAsync(CreateVersion("version-1", latest: false, published: true));
        var services = CreateServices(contentManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DiscardContentItemDraftTool(), services, new()
        {
            ["contentItemId"] = "item-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("discarded").GetBoolean());
        Assert.Equal("version-2", json.GetProperty("discardedVersionId").GetString());
        Assert.True(json.GetProperty("publishedVersionIsLatest").GetBoolean());
        contentManager.Verify(manager => manager.DiscardDraftAsync(draft), Times.Once);

        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("DeleteContent", check.Permission);
        Assert.Same(draft, check.Resource);
    }

    [Fact]
    public async Task DiscardContentItemDraft_WhenTheLatestVersionIsPublished_DiscardsNothing()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("item-1", VersionOptions.Latest)).ReturnsAsync(CreateVersion("version-1", latest: true, published: true));
        var services = CreateServices(contentManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DiscardContentItemDraftTool(), services, new()
        {
            ["contentItemId"] = "item-1",
        });

        // Assert
        Assert.StartsWith("Nothing was discarded", result, StringComparison.Ordinal);
        contentManager.Verify(manager => manager.DiscardDraftAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    private static ContentItem CreateVersion(string versionId, bool latest, bool published)
    {
        return new ContentItem
        {
            ContentItemId = "item-1",
            ContentItemVersionId = versionId,
            ContentType = "Page",
            DisplayText = "About",
            Latest = latest,
            Published = published,
            Author = "jane",
            CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
            ModifiedUtc = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
        };
    }

    private static ServiceProvider CreateServices(IContentManager contentManager)
    {
        var services = AgentToolTestContext.CreateServices();

        services.AddSingleton(contentManager);

        return services.BuildServiceProvider();
    }
}
