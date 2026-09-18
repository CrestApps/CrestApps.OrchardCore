using CrestApps.OrchardCore.Core.Hosting;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Users;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Core.Hosting;

/// <summary>
/// Pins how Orchard Core's users are projected for the suite.
/// </summary>
/// <remarks>
/// The name this produces is shown next to a live call, so the two properties that matter are that a
/// missing user is <see langword="null"/> rather than a blank row, and that there is always a name to
/// render even on a tenant with no display-name provider enabled.
/// </remarks>
public sealed class OrchardCoreUserDirectoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task FindByIdAsync_WithoutAnIdentifier_ReturnsNothing(string userId)
    {
        // Arrange
        var directory = CreateDirectory(out var userManager);

        // Act
        var user = await directory.FindByIdAsync(userId, TestContext.Current.CancellationToken);

        // Assert: the lookup is skipped entirely rather than asking the host for user "".
        Assert.Null(user);
        userManager.Verify(manager => manager.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task FindByNameAsync_WithoutAName_ReturnsNothing(string userName)
    {
        // Arrange
        var directory = CreateDirectory(out var userManager);

        // Act
        var user = await directory.FindByNameAsync(userName, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(user);
        userManager.Verify(manager => manager.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FindByIdAsync_WhenNoSuchUserExists_ReturnsNothing()
    {
        // Arrange
        var directory = CreateDirectory(out var userManager);
        userManager
            .Setup(manager => manager.FindByIdAsync("missing"))
            .ReturnsAsync((IUser)null);

        // Act
        var user = await directory.FindByIdAsync("missing", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task FindByIdAsync_WithNoDisplayNameProvider_FallsBackToTheSignInName()
    {
        // Arrange
        var directory = CreateDirectory(out var userManager);
        SetupUser(userManager, "user-1", "agent.one");

        // Act
        var user = await directory.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        // Assert: a Contact Center tenant that has not enabled the display-name feature still gets a
        // name to put on screen.
        Assert.NotNull(user);
        Assert.Equal("user-1", user.Id);
        Assert.Equal("agent.one", user.UserName);
        Assert.Equal("agent.one", user.DisplayName);
    }

    [Fact]
    public async Task FindByIdAsync_WhenTheProviderHasNoName_FallsBackToTheSignInName()
    {
        // Arrange
        var displayNameProvider = new Mock<IDisplayNameProvider>();
        displayNameProvider
            .Setup(provider => provider.GetAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("   ");

        var directory = CreateDirectory(out var userManager, displayNameProvider.Object);
        SetupUser(userManager, "user-1", "agent.one");

        // Act
        var user = await directory.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("agent.one", user.DisplayName);
    }

    [Fact]
    public async Task FindByIdAsync_WhenTheProviderHasAName_UsesIt()
    {
        // Arrange
        var displayNameProvider = new Mock<IDisplayNameProvider>();
        displayNameProvider
            .Setup(provider => provider.GetAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Agent One");

        var directory = CreateDirectory(out var userManager, displayNameProvider.Object);
        SetupUser(userManager, "user-1", "agent.one");

        // Act
        var user = await directory.FindByIdAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Agent One", user.DisplayName);
        Assert.Equal("agent.one", user.UserName);
    }

    [Fact]
    public async Task GetAsync_WithoutIdentifiers_Throws()
    {
        // Arrange
        var directory = CreateDirectory(out _);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => directory.GetAsync(null, TestContext.Current.CancellationToken));
    }

    private static void SetupUser(Mock<UserManager<IUser>> userManager, string userId, string userName)
    {
        var user = new FakeUser { UserName = userName };

        userManager
            .Setup(manager => manager.FindByIdAsync(userId))
            .ReturnsAsync(user);
        userManager
            .Setup(manager => manager.GetUserIdAsync(user))
            .ReturnsAsync(userId);
        userManager
            .Setup(manager => manager.GetUserNameAsync(user))
            .ReturnsAsync(userName);
        userManager
            .Setup(manager => manager.GetEmailAsync(user))
            .ReturnsAsync($"{userName}@example.com");
    }

    private static OrchardCoreUserDirectory CreateDirectory(
        out Mock<UserManager<IUser>> userManager,
        IDisplayNameProvider displayNameProvider = null)
    {
        var store = new Mock<IUserStore<IUser>>();

        userManager = new Mock<UserManager<IUser>>(
            store.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        return new OrchardCoreUserDirectory(
            userManager.Object,
            new Mock<ISession>().Object,
            displayNameProvider is null ? [] : [displayNameProvider]);
    }
}
