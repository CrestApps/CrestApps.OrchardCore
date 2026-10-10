using System.Net;
using CrestApps.OrchardCore.TenantHierarchy.Handlers;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Covers what keeps a linked user's session in a child tenant in line with the parent: roles that change while the
/// session is open, a linked user signed in without delegated access, and external sign-in of a linked user.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class LinkedSessionTests
{
    private const string BusinessOneAddress = "http://business1.firma.localhost";

    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedSessionTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public LinkedSessionTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OpenSession_WhenItsRolesChangeInTheParent_GetsTheNewRolesAtTheNextCheck_WithoutSigningInAgain()
    {
        // Arrange: a parent administrator, checked every second, who opens a child as Administrator.
        await UpdatePolicyAsync(policy => policy.SessionValidationInterval = TimeSpan.FromSeconds(1));
        var userName = $"roles{Guid.NewGuid():N}"[..20];
        await CreateParentAdministratorAsync(userName);
        string grantId = null;

        try
        {
            using var browser = _fixture.Host.CreateBrowser();
            await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, userName, _fixture.Password);
            await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
            Assert.DoesNotContain("Editor", await GetLinkedRolesAsync(userName));

            // Act: the parent gives this user the Editor role in this child too.
            grantId = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
            {
                var manager = services.GetRequiredService<AccessGrantManager>();
                var result = await manager.AddAsync(AccessGrantPrincipalType.User, userName, _fixture.BusinessOne.EntryId, ["Editor"]);
                Assert.True(result.Succeeded, result.Error);

                return (await manager.ListAsync(_fixture.BusinessOne.EntryId)).Single(grant => grant.PrincipalName == userName).GrantId;
            });

            await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
            var response = await browser.GetAsync($"{BusinessOneAddress}/Admin");

            // Assert: still signed in, now with both roles.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var roles = await GetLinkedRolesAsync(userName);
            Assert.Contains("Administrator", roles);
            Assert.Contains("Editor", roles);
        }
        finally
        {
            if (grantId is not null)
            {
                await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services => services.GetRequiredService<AccessGrantManager>().RemoveAsync(grantId));
            }

            await UpdatePolicyAsync(policy => policy.SessionValidationInterval = TimeSpan.FromMinutes(2));
        }
    }

    [Fact]
    public async Task LinkedUser_SignedInWithoutDelegatedAccess_IsSignedOut_ButALocalUserIsNot()
    {
        // Arrange: Alice has a linked user in Business One once she opened it in a browser.
        await OpenBusinessOneAsAliceAsync();
        var localName = $"local{Guid.NewGuid():N}"[..16];
        await CreateChildUserAsync(localName);

        // Act
        var (linked, local) = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
            (await ValidateWithoutDelegatedAccessAsync(services, $"{TenantHierarchyFixture.Alice}+firma"),
             await ValidateWithoutDelegatedAccessAsync(services, localName)));

        // Assert
        Assert.Null(linked);
        Assert.NotNull(local);
    }

    [Fact]
    public async Task ExternalSignIn_OfALinkedUser_IsForbidden_ButOfALocalUserIsLeftAlone()
    {
        // Arrange
        await OpenBusinessOneAsAliceAsync();
        var localName = $"local{Guid.NewGuid():N}"[..16];
        await CreateChildUserAsync(localName);

        // Act
        var (linked, local, emptyName) = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var loginEvent = services.GetServices<ILoginFormEvent>().OfType<LinkedUserLoginFormEvent>().Single();
            var errors = new List<string>();

            var forLinked = await loginEvent.ValidatingLoginAsync(await userManager.FindByNameAsync($"{TenantHierarchyFixture.Alice}+firma"));
            var forLocal = await loginEvent.ValidatingLoginAsync(await userManager.FindByNameAsync(localName));
            await loginEvent.LoggingInAsync(string.Empty, (_, message) => errors.Add(message));

            return (forLinked, forLocal, errors);
        });

        // Assert
        Assert.IsType<ForbidResult>(linked);
        Assert.Null(local);
        Assert.Empty(emptyName);
    }

    private async Task OpenBusinessOneAsAliceAsync()
    {
        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Alice, _fixture.Password);
        await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
    }

    private static async Task<System.Security.Claims.ClaimsPrincipal> ValidateWithoutDelegatedAccessAsync(IServiceProvider services, string userName)
    {
        var user = await services.GetRequiredService<UserManager<IUser>>().FindByNameAsync(userName);
        var principal = await services.GetRequiredService<SignInManager<IUser>>().CreateUserPrincipalAsync(user);
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var scheme = new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler));
        var context = new CookieValidatePrincipalContext(
            new DefaultHttpContext { RequestServices = services },
            scheme,
            options,
            new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme));

        await services.GetRequiredService<DelegatedSessionPrincipalValidator>().ValidateAsync(context);

        return context.Principal;
    }

    private Task<IList<string>> GetLinkedRolesAsync(string parentUserName)
        => _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();

            return await userManager.GetRolesAsync(await userManager.FindByNameAsync($"{parentUserName}+firma"));
        });

    private Task CreateParentAdministratorAsync(string userName)
        => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = new User { UserName = userName, Email = $"{userName}@example.com", EmailConfirmed = true, IsEnabled = true };
            Assert.True((await userManager.CreateAsync(user, _fixture.Password)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, "Administrator")).Succeeded);
        });

    private Task CreateChildUserAsync(string userName)
        => _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var user = new User { UserName = userName, Email = $"{userName}@example.com", EmailConfirmed = true, IsEnabled = true };
            Assert.True((await services.GetRequiredService<UserManager<IUser>>().CreateAsync(user, _fixture.Password)).Succeeded);
        });

    private Task UpdatePolicyAsync(Action<ParentTenantPolicy> change)
        => _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var settings = _fixture.Host.GetSettings(TenantHierarchyFixture.FirmA);
            var policy = settings.GetParentPolicy();
            change(policy);
            var (result, _) = await services.GetRequiredService<TenantHierarchyPlatformService>().UpdateParentAsync(TenantHierarchyFixture.FirmA, settings.GetHierarchyDisplayName(), policy);
            Assert.True(result.Succeeded, result.Error);
        });
}
