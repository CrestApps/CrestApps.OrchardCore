using System.Security.Claims;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Proves goal G4: a child administrator cannot act as a parent user. A linked user has no password, cannot sign in
/// with one, and cannot be edited, deleted or given roles from the child.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class LinkedUserProtectionTests
{
    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedUserProtectionTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public LinkedUserProtectionTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Provision_CreatesOneLinkedUserPerParentUser_WithTheGrantedRolesAndNoPassword()
    {
        // Act: the same parent user enters twice.
        var first = await ProvisionAsync();
        var second = await ProvisionAsync();

        // Assert
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal("alice+firma", second.UserName);
        Assert.Null(second.PasswordHash);
        Assert.True(second.EmailConfirmed);
        Assert.Contains("Administrator", second.RoleNames);
    }

    [Fact]
    public async Task Provision_AfterTheLinkedUserWasDeleted_CreatesANewOneAndKeepsTheOldLink()
    {
        // Arrange
        var original = await ProvisionAsync();
        await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            await userManager.DeleteAsync(await userManager.FindByIdAsync(original.UserId));
        });

        // Act
        var replacement = await ProvisionAsync();

        // Assert
        Assert.NotEqual(original.UserId, replacement.UserId);
        var oldLink = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, services =>
            services.GetRequiredService<UserLinkStore>().FindByChildUserIdAsync(original.UserId));
        Assert.NotNull(oldLink);
        Assert.False(oldLink.IsCurrent);
    }

    [Fact]
    public async Task PasswordSetByRecipe_StillCannotSignIn()
    {
        // Arrange: a child administrator writes a password hash straight into the store, as the Users recipe step does.
        var linked = await ProvisionAsync();
        await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var hasher = services.GetRequiredService<IPasswordHasher<IUser>>();
            var session = services.GetRequiredService<ISession>();
            var user = (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(linked.UserId);
            user.PasswordHash = hasher.HashPassword(user, _fixture.Password);
            await session.SaveAsync(user);
        });

        using var browser = _fixture.Host.CreateBrowser();

        // Act
        var response = await browser.SignInAsync("http://business1.firma.localhost", linked.UserName, _fixture.Password);

        // Assert: the login form is shown again and no authentication cookie is set.
        Assert.False(response.Headers.Location?.ToString().Contains("/Admin", StringComparison.OrdinalIgnoreCase) == true);
        Assert.False(browser.HasCookie("business1.firma.localhost", "orchauth_"));
    }

    [Fact]
    public async Task UpdatingALinkedUser_RemovesAnyPasswordHash()
    {
        // Arrange
        var linked = await ProvisionAsync();

        // Act
        var hash = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = (User)await userManager.FindByIdAsync(linked.UserId);
            user.PasswordHash = "AQAAAAEAACcQAAAAEFakeHash";
            await userManager.UpdateAsync(user);

            return ((User)await userManager.FindByIdAsync(linked.UserId)).PasswordHash;
        });

        // Assert
        Assert.Null(hash);
    }

    [Theory]
    [InlineData("EditUsers")]
    [InlineData("DeleteUsers")]
    [InlineData("AssignRoleToUsers")]
    [InlineData("ManageUsers")]
    public async Task UserManagementPermission_OnALinkedUser_IsDeniedEvenToAnAdministrator(string permissionName)
    {
        // Arrange
        var linked = await ProvisionAsync();

        // Act
        var (onLinked, onLocal) = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var authorization = services.GetRequiredService<IAuthorizationService>();
            var permission = new Permission(permissionName);
            var administrator = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "local-admin"),
                    new Claim(ClaimTypes.Name, "local-admin"),
                    new Claim(ClaimTypes.Role, "Administrator"),
                ],
                "Test"));

            var local = await EnsureLocalUserAsync(userManager);
            var linkedUser = await userManager.FindByIdAsync(linked.UserId);

            return (
                await authorization.AuthorizeAsync(administrator, permission, linkedUser),
                await authorization.AuthorizeAsync(administrator, permission, local));
        });

        // Assert
        Assert.False(onLinked);
        Assert.True(onLocal);
    }

    [Fact]
    public async Task IsLinkedUser_ForALocalUser_IsFalse()
    {
        // Act
        var isLinked = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var local = await EnsureLocalUserAsync(services.GetRequiredService<UserManager<IUser>>());

            return await services.GetRequiredService<LinkedUserService>().IsLinkedUserAsync(((User)local).UserId);
        });

        // Assert
        Assert.False(isLinked);
    }

    private async Task<User> ProvisionAsync()
    {
        var redemption = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);

        return await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, services =>
            services.GetRequiredService<LinkedUserService>().ProvisionAsync(redemption));
    }

    private async Task<IUser> EnsureLocalUserAsync(UserManager<IUser> userManager)
    {
        var local = await userManager.FindByNameAsync("local-staff");

        if (local is not null)
        {
            return local;
        }

        local = new User
        {
            UserName = "local-staff",
            Email = "local-staff@example.invalid",
            EmailConfirmed = true,
            IsEnabled = true,
        };

        var result = await userManager.CreateAsync(local, _fixture.Password);
        Assert.True(result.Succeeded, string.Join(' ', result.Errors.Select(error => error.Description)));

        return local;
    }
}
