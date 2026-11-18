using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Users.AI;
using CrestApps.OrchardCore.Users.Core.Models;
using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users.AI;

/// <summary>
/// The user name and avatar tools check what the user editor's cards check, with the user as resource, apply the
/// required-name rules and the avatar's image rules, and save through the user manager.
/// </summary>
public sealed class UserProfileAIToolTests
{
    private readonly User _maria = new() { UserId = "user-maria", UserName = "maria", Email = "maria@example.com" };
    private readonly List<User> _saved = [];
    private DisplayNameSettings _displayNameSettings = new();
    private bool _avatarRequired;

    public static TheoryData<string, string[]> Tools => new()
    {
        { SetUserDisplayNameTool.TheName, ["EditUsers"] },
        { SetUserAvatarTool.TheName, ["ManageMediaContent", "EditUsers"] },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string[] permissions)
    {
        // Arrange
        _ = permissions;
        var services = Build(signedIn: false);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(UsersAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_saved);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutEachPermission(string toolName, string[] permissions)
    {
        foreach (var missing in permissions)
        {
            // Arrange
            var services = Build(authorize: (permission, _) => permission.Name != missing);

            // Act
            var result = await InvokeAsync(toolName, services, Arguments(toolName));

            // Assert
            Assert.Equal(UsersAIToolBase.NotAllowedMessage, result);
            Assert.Empty(_saved);
        }
    }

    [Fact]
    public async Task EditUsers_IsCheckedWithTheUserAsResource()
    {
        // Arrange
        var services = Build(authorize: (permission, resource) => permission.Name != "EditUsers" || resource is null || resource == _maria);

        // Act
        await InvokeAsync(SetUserDisplayNameTool.TheName, services, Arguments(SetUserDisplayNameTool.TheName));

        // Assert
        Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == "EditUsers" && check.Resource == _maria);
        Assert.Single(_saved);
    }

    [Fact]
    public async Task SetUserDisplayName_ChangesOnlyTheGivenNames()
    {
        // Arrange
        _maria.Put(new UserFullNamePart { FirstName = "Maria", LastName = "Lopez", DisplayName = "Maria Lopez" });
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SetUserDisplayNameTool.TheName, services, new()
        {
            ["userId"] = "user-maria",
            ["displayName"] = "Maria L.",
            ["lastName"] = "Lopez-Garcia",
        }));

        // Assert
        var part = Assert.Single(_saved).GetOrCreate<UserFullNamePart>();

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("Maria", part.FirstName);
        Assert.Equal("Lopez-Garcia", part.LastName);
        Assert.Equal("Maria L.", part.DisplayName);
    }

    [Fact]
    public async Task SetUserDisplayName_RefusesToClearARequiredName()
    {
        // Arrange
        _displayNameSettings = new DisplayNameSettings { FirstName = DisplayNamePropertyType.Required };
        _maria.Put(new UserFullNamePart { FirstName = "Maria" });
        var services = Build();

        // Act
        var result = await InvokeAsync(SetUserDisplayNameTool.TheName, services, new() { ["userId"] = "user-maria", ["firstName"] = "" });

        // Assert
        Assert.Contains("require firstName", result, StringComparison.Ordinal);
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task SetUserDisplayName_WithAnUnknownUser_SaysSo()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await InvokeAsync(SetUserDisplayNameTool.TheName, services, new() { ["userId"] = "missing", ["firstName"] = "X" });

        // Assert
        Assert.Contains("No user has the id 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetUserAvatar_SetsAnExistingImage_GivenItsPublicUrl()
    {
        // Arrange
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SetUserAvatarTool.TheName, services, new()
        {
            ["userId"] = "maria",
            ["mediaPath"] = "/media/people/ana.png",
        }));

        // Assert
        var part = Assert.Single(_saved).GetOrCreate<UserAvatarPart>();

        Assert.Equal("people/ana.png", json.GetProperty("avatarPath").GetString());
        Assert.Equal(new[] { "people/ana.png" }, part.Avatar.Paths);
    }

    [Theory]
    [InlineData("people/missing.png", "there is no file")]
    [InlineData("docs/cv.pdf", "is not an image")]
    [InlineData("../secrets/key.png", "is not a media library path")]
    public async Task SetUserAvatar_RefusesAFileThatCannotBeAnAvatar(string mediaPath, string expected)
    {
        // Arrange
        var services = Build();

        // Act
        var result = await InvokeAsync(SetUserAvatarTool.TheName, services, new() { ["userId"] = "user-maria", ["mediaPath"] = mediaPath });

        // Assert
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(_saved);
    }

    [Fact]
    public async Task SetUserAvatar_RefusesToClearARequiredAvatar()
    {
        // Arrange
        _avatarRequired = true;
        var services = Build();

        // Act
        var result = await InvokeAsync(SetUserAvatarTool.TheName, services, new() { ["userId"] = "user-maria", ["clear"] = true });

        // Assert
        Assert.Contains("requires an avatar", result, StringComparison.Ordinal);
        Assert.Empty(_saved);
    }

    private ServiceProvider Build(bool signedIn = true, Func<global::OrchardCore.Security.Permissions.Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((string id) => id == _maria.UserId ? _maria : null);
        userManager.Setup(manager => manager.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((string name) => name == _maria.UserName ? _maria : null);
        userManager.Setup(manager => manager.UpdateAsync(It.IsAny<IUser>()))
            .Callback((IUser user) => _saved.Add((User)user))
            .ReturnsAsync(IdentityResult.Success);
        services.AddSingleton(userManager.Object);

        var site = new SiteSettings();
        site.Put(_displayNameSettings);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);
        services.AddSingleton(siteService.Object);

        var image = new Mock<IFileStoreEntry>();
        image.SetupGet(entry => entry.Path).Returns("people/ana.png");
        var mediaFileStore = new Mock<IMediaFileStore>();
        mediaFileStore.Setup(store => store.MapPathToPublicUrl(It.IsAny<string>())).Returns((string path) => "/media/" + path);
        mediaFileStore.Setup(store => store.GetFileInfoAsync(It.IsAny<string>())).ReturnsAsync((string path) => path == "people/ana.png" ? image.Object : null);
        services.AddSingleton(mediaFileStore.Object);

        services.AddSingleton(Options.Create(new MediaOptions { AllowedFileExtensions = [".png", ".jpg", ".pdf"] }));
        services.AddSingleton<IContentTypeProvider>(new FileExtensionContentTypeProvider());
        services.AddSingleton(Mock.Of<IOptionsMonitor<UserAvatarOptions>>(monitor => monitor.CurrentValue == new UserAvatarOptions { Required = _avatarRequired }));

        return services.BuildServiceProvider();
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName == SetUserAvatarTool.TheName
            ? new() { ["userId"] = "user-maria", ["mediaPath"] = "people/ana.png" }
            : new() { ["userId"] = "user-maria", ["displayName"] = "Maria L." };

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, arguments);

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            SetUserDisplayNameTool.TheName => new SetUserDisplayNameTool(),
            SetUserAvatarTool.TheName => new SetUserAvatarTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
