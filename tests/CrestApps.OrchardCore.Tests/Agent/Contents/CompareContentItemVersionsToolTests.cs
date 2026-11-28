using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

/// <summary>
/// compareContentItemVersions lists the values two versions of an item differ in, checks the right to edit the item on
/// its latest version, and only compares versions of that item.
/// </summary>
public sealed class CompareContentItemVersionsToolTests
{
    [Fact]
    public async Task CompareContentItemVersions_ByDefault_ComparesTheDraftWithThePublishedVersion()
    {
        // Arrange
        var first = CreateVersion(1, "version-1", latest: false, published: false, title: "Old about");
        var published = CreateVersion(2, "version-2", latest: false, published: true, title: "About");
        var draft = CreateVersion(3, "version-3", latest: true, published: false, title: "About us");

        ((JsonObject)draft.Content)["HtmlBodyPart"] = new JsonObject { ["Html"] = "<p>We build things.</p>" };

        var services = CreateServices(first, published, draft);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompareContentItemVersionsTool(), services, new()
        {
            ["contentItemId"] = "item-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var changes = json.GetProperty("changes").EnumerateArray().ToList();
        var title = Assert.Single(changes, change => change.GetProperty("path").GetString() == "TitlePart.Title");
        var body = Assert.Single(changes, change => change.GetProperty("path").GetString() == "HtmlBodyPart.Html");

        Assert.Equal("version-2", json.GetProperty("from").GetProperty("contentItemVersionId").GetString());
        Assert.Equal("version-3", json.GetProperty("to").GetProperty("contentItemVersionId").GetString());
        Assert.Equal("changed", title.GetProperty("change").GetString());
        Assert.Equal("About", title.GetProperty("before").GetString());
        Assert.Equal("About us", title.GetProperty("after").GetString());
        Assert.Equal("added", body.GetProperty("change").GetString());
        Assert.False(json.GetProperty("truncated").GetBoolean());

        // The version flags and dates are reported per version, not as changes.
        Assert.DoesNotContain(changes, change => change.GetProperty("path").GetString() is "Latest" or "Published" or "ContentItemVersionId");

        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("EditContent", check.Permission);
        Assert.Same(draft, check.Resource);
    }

    [Fact]
    public async Task CompareContentItemVersions_GivenTwoVersions_ComparesThoseVersions()
    {
        // Arrange
        var first = CreateVersion(1, "version-1", latest: false, published: false, title: "Old about");
        var published = CreateVersion(2, "version-2", latest: true, published: true, title: "About");
        var services = CreateServices(first, published);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompareContentItemVersionsTool(), services, new()
        {
            ["contentItemId"] = "item-1",
            ["fromVersionId"] = "version-1",
            ["toVersionId"] = "version-2",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var displayText = Assert.Single(json.GetProperty("changes").EnumerateArray(), change => change.GetProperty("path").GetString() == "DisplayText");

        Assert.Equal("Old about", displayText.GetProperty("before").GetString());
        Assert.Contains("restoreContentItemVersion", json.GetProperty("note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompareContentItemVersions_ForAVersionOfAnotherItem_SaysSo()
    {
        // Arrange
        var published = CreateVersion(2, "version-2", latest: true, published: true, title: "About");
        var services = CreateServices(published);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompareContentItemVersionsTool(), services, new()
        {
            ["contentItemId"] = "item-1",
            ["fromVersionId"] = "version-of-another-item",
        });

        // Assert
        Assert.StartsWith("Content item 'item-1' has no version 'version-of-another-item'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompareContentItemVersions_ForTheOnlyVersion_SaysThereIsNothingToCompare()
    {
        // Arrange
        var published = CreateVersion(1, "version-1", latest: true, published: true, title: "About");
        var services = CreateServices(published);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompareContentItemVersionsTool(), services, new()
        {
            ["contentItemId"] = "item-1",
        });

        // Assert
        Assert.Contains("is the first version", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompareContentItemVersions_WhenTheCallerMayNotEditTheItem_Refuses()
    {
        // Arrange
        var first = CreateVersion(1, "version-1", latest: false, published: false, title: "Old about");
        var published = CreateVersion(2, "version-2", latest: true, published: true, title: "About");
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAllVersionsAsync("item-1")).ReturnsAsync([first, published]);

        var services = AgentToolTestContext.CreateServices(authorize: (_, _) => false);
        services.AddSingleton(contentManager.Object);

        using var provider = services.BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompareContentItemVersionsTool(), provider, new()
        {
            ["contentItemId"] = "item-1",
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    private static ContentItem CreateVersion(long id, string versionId, bool latest, bool published, string title)
    {
        var version = new ContentItem
        {
            Id = id,
            ContentItemId = "item-1",
            ContentItemVersionId = versionId,
            ContentType = "Page",
            DisplayText = title,
            Latest = latest,
            Published = published,
            Author = "jane",
            CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
            ModifiedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc).AddDays(id),
        };

        ((JsonObject)version.Content)["TitlePart"] = new JsonObject { ["Title"] = title };

        return version;
    }

    private static ServiceProvider CreateServices(params ContentItem[] versions)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAllVersionsAsync("item-1")).ReturnsAsync(versions);

        var services = AgentToolTestContext.CreateServices();

        services.AddSingleton(contentManager.Object);

        return services.BuildServiceProvider();
    }
}
