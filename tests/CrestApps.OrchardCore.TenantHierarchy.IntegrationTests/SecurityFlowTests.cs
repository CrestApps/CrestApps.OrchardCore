using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Drives the access rules of a hierarchy end to end, over HTTP where a browser would: two-factor enforcement, the
/// ways an open child session ends, the tenant picker actions, the address check and the platform's detach actions.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed partial class SecurityFlowTests
{
    private const string BusinessOneAddress = "http://business1.firma.localhost";

    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityFlowTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public SecurityFlowTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OpenChild_WhenThePolicyRequiresTwoFactor_AndTheUserSignedInWithAPassword_IsRefused()
    {
        // Arrange
        await UpdatePolicyAsync(policy => policy.RequireMfa = true);

        try
        {
            using var browser = await SignInAsAliceAsync();

            // Act
            var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            // Assert: the user is told why, and no session reaches the child.
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("two-factor authentication", html, StringComparison.Ordinal);
            Assert.False(browser.HasCookie("business1.firma.localhost", "orchauth_"));
        }
        finally
        {
            await UpdatePolicyAsync(policy => policy.RequireMfa = false);
        }
    }

    [Fact]
    public async Task SigningOutOfTheParent_EndsTheChildSessionsThatSignInStarted()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
        var aliceId = await GetUserIdAsync(TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice);
        var before = await ListOpenSessionsAsync(aliceId);

        // Act: the browser posts the admin's log off form, as a person clicking "Log off" would.
        var (action, token) = await GetLogOffFormAsync(browser);
        var logOff = await browser.PostAsync($"{TenantHierarchyFixture.FirmAAddress}{action}", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        });

        // Assert
        Assert.True(logOff.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.OK, $"Log off returned {(int)logOff.StatusCode}.");
        var after = await ListOpenSessionsAsync(aliceId);
        var ended = before.Select(session => session.SessionHash).Except(after.Select(session => session.SessionHash)).ToList();
        Assert.NotEmpty(ended);
    }

    [Fact]
    public async Task OpenChildSession_AfterTheParentUserChangesTheirSecurityStamp_IsSignedOutAtTheNextCheck()
    {
        // Arrange: check the session every second, so the test does not wait minutes.
        await UpdatePolicyAsync(policy => policy.SessionValidationInterval = TimeSpan.FromSeconds(1));
        var userName = $"stamp{Guid.NewGuid():N}"[..20];
        await CreateParentAdministratorAsync(userName);

        try
        {
            using var browser = _fixture.Host.CreateBrowser();
            await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, userName, _fixture.Password);
            await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"{BusinessOneAddress}/Admin")).StatusCode);

            // Act: what changing the password or signing out everywhere does to the parent account.
            await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
            {
                var userManager = services.GetRequiredService<UserManager<IUser>>();
                await userManager.UpdateSecurityStampAsync(await userManager.FindByNameAsync(userName));
            });

            await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
            var afterwards = await browser.GetAsync($"{BusinessOneAddress}/Admin");

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, afterwards.StatusCode);
            Assert.Contains("Login", afterwards.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await UpdatePolicyAsync(policy => policy.SessionValidationInterval = TimeSpan.FromMinutes(2));
        }
    }

    [Fact]
    public async Task Favorite_TogglesTheChildInTheUsersPreferences()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        var aliceId = await GetUserIdAsync(TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice);
        var token = await browser.GetAntiforgeryTokenAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/switch");
        var url = $"{TenantHierarchyFixture.FirmAAddress}/delegated-access/favorite/{_fixture.BusinessTwo.EntryId}";
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };

        // Act
        await browser.PostAsync(url, fields);
        var afterFirst = await GetFavoritesAsync(aliceId);
        await browser.PostAsync(url, fields);
        var afterSecond = await GetFavoritesAsync(aliceId);

        // Assert
        Assert.Contains(_fixture.BusinessTwo.EntryId, afterFirst);
        Assert.DoesNotContain(_fixture.BusinessTwo.EntryId, afterSecond);
    }

    [Fact]
    public async Task Favorite_OfAChildTheUserMayNotOpen_IsNotFound()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        var token = await browser.GetAntiforgeryTokenAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/switch");

        // Act: Business Four belongs to another parent.
        var response = await browser.PostAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/favorite/{_fixture.BusinessFour.EntryId}", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SignOutEverywhere_EndsEveryOpenSessionOfTheUser()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
        await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessTwo.EntryId}");
        var aliceId = await GetUserIdAsync(TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice);
        Assert.NotEmpty(await ListOpenSessionsAsync(aliceId));
        var token = await browser.GetAntiforgeryTokenAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/switch");

        // Act
        var response = await browser.PostAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/sign-out-everywhere", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(await ListOpenSessionsAsync(aliceId));
    }

    [Theory]
    [InlineData("business1", false)]
    [InlineData("admin", false)]
    [InlineData("Not A Slug", false)]
    [InlineData("a-free-address", true)]
    public async Task CheckAddress_TellsWhetherAnAddressCanBeUsed(string slug, bool available)
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/check-address?slug={Uri.EscapeDataString(slug)}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(available, json.RootElement.GetProperty("available").GetBoolean());

        if (available)
        {
            Assert.Equal($"http://{slug}.firma.localhost", json.RootElement.GetProperty("address").GetString());
        }
    }

    [Fact]
    public async Task Status_OfAChild_ReportsItsState_AndOfAnotherParentsChild_LooksLikeAnUnknownOne()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var own = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/{_fixture.BusinessOne.EntryId}/status");
        var foreign = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/{_fixture.BusinessFour.EntryId}/status");
        var unknown = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/{Guid.NewGuid():N}/status");
        using var json = JsonDocument.Parse(await own.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var foreignBody = await foreign.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert: the list refresh drops a row whose child is gone; another parent's child reveals nothing more.
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("Ready", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(unknownBody, foreignBody);
        Assert.DoesNotContain("Business Four", foreignBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnmakeParent_WithoutChildren_MakesItAnOrdinaryTenantAndWithChildren_IsRefused()
    {
        // Arrange
        var name = $"solo{Guid.NewGuid():N}"[..12];
        await _fixture.Host.CreateTenantAsync(name, $"{name}.localhost");
        await _fixture.Host.SetupTenantAsync(name, "Blank", "owner", _fixture.Password);

        // Act
        var (withChildren, ordinary) = await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var platform = services.GetRequiredService<TenantHierarchyPlatformService>();
            var (made, _) = await platform.MakeParentAsync(name, name, "Solo", new ParentTenantPolicy());
            Assert.True(made.Succeeded, made.Error);

            return (await platform.UnmakeParentAsync(TenantHierarchyFixture.FirmA), await platform.UnmakeParentAsync(name));
        });

        // Assert
        Assert.False(withChildren.Succeeded);
        Assert.True(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmA).IsParentTenant());
        Assert.True(ordinary.Succeeded, ordinary.Error);
        Assert.False(_fixture.Host.GetSettings(name).IsInTenantHierarchy());
    }

    [Fact]
    public async Task DetachChild_MakesItAnOrdinaryTenantThatKeepsItsAddress_AndTheParentIsFlagged()
    {
        // Arrange: a parent of its own, so the shared parents stay consistent for the other tests.
        var parent = $"det{Guid.NewGuid():N}"[..12];
        await _fixture.Host.CreateTenantAsync(parent, $"{parent}.localhost");
        await _fixture.Host.SetupTenantAsync(parent, "Blank", "owner", _fixture.Password);
        await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var (made, _) = await services.GetRequiredService<TenantHierarchyPlatformService>().MakeParentAsync(parent, parent, "Detach Parent", new ParentTenantPolicy());
            Assert.True(made.Succeeded, made.Error);
        });

        var entry = await _fixture.CreateChildAsync(parent, "Detached Business", "detached");
        var host = _fixture.Host.GetSettings(entry.TenantName).RequestUrlHost;

        // Act
        var (result, detail) = await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var platform = services.GetRequiredService<TenantHierarchyPlatformService>();
            var detached = await platform.DetachChildAsync(entry.TenantName);

            return (detached, await platform.GetParentDetailAsync(parent));
        });

        // Assert: the tenant keeps its data and address, and the parent's registry entry that no longer matches is shown.
        Assert.True(result.Succeeded, result.Error);
        var settings = _fixture.Host.GetSettings(entry.TenantName);
        Assert.False(settings.IsInTenantHierarchy());
        Assert.Equal(host, settings.RequestUrlHost);
        Assert.NotEmpty(detail.Warnings);
    }

    private async Task<TestBrowser> SignInAsAliceAsync()
    {
        var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Alice, _fixture.Password);

        return browser;
    }

    private static async Task<(string Action, string Token)> GetLogOffFormAsync(TestBrowser browser)
    {
        var page = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin");
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var form = LogOffFormPattern().Match(html);

        Assert.True(form.Success, "The admin page has no log off form.");

        return (WebUtility.HtmlDecode(form.Groups["action"].Value), form.Groups["token"].Value);
    }

    [GeneratedRegex(@"<form[^>]*action=""(?<action>[^""]*LogOff[^""]*)""[^>]*>.*?name=""__RequestVerificationToken""[^>]*value=""(?<token>[^""]+)""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex LogOffFormPattern();

    private Task<string> GetUserIdAsync(string tenant, string userName)
        => _fixture.Host.InTenantAsync(tenant, async services =>
            ((User)await services.GetRequiredService<UserManager<IUser>>().FindByNameAsync(userName)).UserId);

    private Task<IReadOnlyList<DelegatedAccessSession>> ListOpenSessionsAsync(string parentUserId)
        => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, services =>
            services.GetRequiredService<DelegatedAccessSessionStore>().ListOpenByUserAsync(parentUserId));

    private Task<List<string>> GetFavoritesAsync(string userId)
        => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
            (await services.GetRequiredService<TenantSwitcherPreferenceStore>().GetAsync(userId))?.Favorites?.ToList() ?? []);

    private Task CreateParentAdministratorAsync(string userName)
        => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = new User { UserName = userName, Email = $"{userName}@example.com", EmailConfirmed = true, IsEnabled = true };
            var created = await userManager.CreateAsync(user, _fixture.Password);
            Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Description)));
            await userManager.AddToRoleAsync(user, "Administrator");
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
