using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A profile made on an agent's first sign-in or presence change carried only the user id, so every workforce report,
/// the timeline and the timecards named the agent "(Unknown agent)". These pin that a profile without a user name takes
/// it from the account when it is saved, that a name already there is left alone, and that the one-time pass saves
/// only the profiles that need it.
/// </summary>
public sealed class AgentProfileUserNameTests
{
    [Fact]
    public async Task Creating_AProfileWithoutAUserName_TakesItFromTheUserAccount()
    {
        // Arrange
        var profile = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var handler = new AgentProfileUserNameHandler(CreateUserManager(("u1", "mike")).Object);

        // Act
        await handler.CreatingAsync(new CreatingContext<AgentProfile>(profile), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("mike", profile.UserName);
    }

    [Fact]
    public async Task Updating_AProfileWithoutAUserName_TakesItFromTheUserAccount()
    {
        // Arrange
        var profile = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var handler = new AgentProfileUserNameHandler(CreateUserManager(("u1", "mike")).Object);

        // Act
        await handler.UpdatingAsync(new UpdatingContext<AgentProfile>(profile, new System.Text.Json.Nodes.JsonObject()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("mike", profile.UserName);
    }

    [Fact]
    public async Task Saving_AProfileThatHasAUserName_LeavesItAndDoesNotLookTheUserUp()
    {
        // Arrange
        var profile = new AgentProfile { ItemId = "a1", UserId = "u1", UserName = "kept" };
        var userManager = CreateUserManager(("u1", "other"));
        var handler = new AgentProfileUserNameHandler(userManager.Object);

        // Act
        await handler.CreatingAsync(new CreatingContext<AgentProfile>(profile), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("kept", profile.UserName);
        userManager.Verify(manager => manager.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Saving_AProfileWhoseUserNoLongerExists_LeavesTheNameEmpty()
    {
        // Arrange
        var profile = new AgentProfile { ItemId = "a1", UserId = "gone" };
        var handler = new AgentProfileUserNameHandler(CreateUserManager().Object);

        // Act
        await handler.CreatingAsync(new CreatingContext<AgentProfile>(profile), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(profile.UserName);
    }

    [Fact]
    public async Task Backfill_SavesOnlyTheProfilesMissingAUserName()
    {
        // Arrange
        var missing = new AgentProfile { ItemId = "a1", UserId = "u1" };
        var named = new AgentProfile { ItemId = "a2", UserId = "u2", UserName = "named" };
        var orphan = new AgentProfile { ItemId = "a3" };
        var manager = new Mock<IAgentProfileManager>();
        manager.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([missing, named, orphan]);

        // Act
        await AgentProfileUserNameBackfill.FillMissingUserNamesAsync(manager.Object);

        // Assert
        manager.Verify(value => value.UpdateAsync(missing, null, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(value => value.UpdateAsync(named, null, It.IsAny<CancellationToken>()), Times.Never);
        manager.Verify(value => value.UpdateAsync(orphan, null, It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<UserManager<IUser>> CreateUserManager(params (string Id, string UserName)[] users)
    {
        var userManager = new Mock<UserManager<IUser>>(
            new Mock<IUserStore<IUser>>().Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        userManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((IUser)null);

        foreach (var (id, userName) in users)
        {
            var user = new Mock<IUser>();
            user.SetupGet(value => value.UserName).Returns(userName);
            userManager.Setup(manager => manager.FindByIdAsync(id)).ReturnsAsync(user.Object);
        }

        return userManager;
    }
}
