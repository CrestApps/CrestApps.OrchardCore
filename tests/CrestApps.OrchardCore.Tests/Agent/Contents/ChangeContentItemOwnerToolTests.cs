using CrestApps.OrchardCore.AI.Agent.Contents;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

/// <summary>
/// The owner tool sets the owner of a content item to a user it finds by name, ID or email, on the latest version and
/// on the published version when the latest is a draft.
/// </summary>
public sealed class ChangeContentItemOwnerToolTests
{
    [Fact]
    public async Task InvokeAsync_WithAUserName_SetsTheOwnerOnTheDraftAndThePublishedVersion()
    {
        // Arrange
        var draft = CreateVersion("version-2", published: false);
        var published = CreateVersion("version-1", published: true);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("item-1", VersionOptions.Latest)).ReturnsAsync(draft);
        contentManager.Setup(manager => manager.GetAsync("item-1", VersionOptions.Published)).ReturnsAsync(published);
        var services = CreateServices(contentManager.Object, CreateUserManager());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ChangeContentItemOwnerTool(), services, new()
        {
            ["contentItemId"] = "item-1",
            ["userName"] = "jane",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("changed").GetBoolean());
        Assert.Equal("old-owner", json.GetProperty("previousOwnerUserId").GetString());
        Assert.Equal("jane-id", json.GetProperty("ownerUserId").GetString());
        Assert.Equal(2, json.GetProperty("updatedVersionIds").GetArrayLength());
        Assert.Equal("jane-id", draft.Owner);
        Assert.Equal("jane-id", published.Owner);
        Assert.Equal("someone", draft.Author);
        contentManager.Verify(manager => manager.UpdateAsync(draft), Times.Once);
        contentManager.Verify(manager => manager.UpdateAsync(published), Times.Once);

        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("EditContentOwner", check.Permission);
        Assert.Same(draft, check.Resource);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoUserMatches_ChangesNothing()
    {
        // Arrange
        var contentItem = CreateVersion("version-1", published: true);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("item-1", VersionOptions.Latest)).ReturnsAsync(contentItem);
        var services = CreateServices(contentManager.Object, CreateUserManager());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ChangeContentItemOwnerTool(), services, new()
        {
            ["contentItemId"] = "item-1",
            ["email"] = "nobody@example.com",
        });

        // Assert
        Assert.StartsWith("Nothing was changed: no user matches", result, StringComparison.Ordinal);
        Assert.Equal("old-owner", contentItem.Owner);
        contentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    private static ContentItem CreateVersion(string versionId, bool published)
    {
        return new ContentItem
        {
            ContentItemId = "item-1",
            ContentItemVersionId = versionId,
            ContentType = "Page",
            DisplayText = "About",
            Latest = !published,
            Published = published,
            Owner = "old-owner",
            Author = "someone",
        };
    }

    private static UserManager<IUser> CreateUserManager()
    {
        var jane = new Mock<IUser>();
        jane.Setup(user => user.UserName).Returns("jane");

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByNameAsync("jane")).ReturnsAsync(jane.Object);
        userManager.Setup(manager => manager.GetUserIdAsync(jane.Object)).ReturnsAsync("jane-id");

        return userManager.Object;
    }

    private static ServiceProvider CreateServices(IContentManager contentManager, UserManager<IUser> userManager)
    {
        var services = AgentToolTestContext.CreateServices();

        services.AddSingleton(contentManager);
        services.AddSingleton(userManager);

        return services.BuildServiceProvider();
    }
}
