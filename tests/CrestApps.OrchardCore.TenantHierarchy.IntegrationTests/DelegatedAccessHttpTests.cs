using System.Net;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Drives delegated access over HTTP, as a browser does: sign in to the parent, open a child, and use it.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class DelegatedAccessHttpTests
{
    private const string BusinessOneAddress = "http://business1.firma.localhost";

    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegatedAccessHttpTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public DelegatedAccessHttpTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OpenChild_SignedInParentUser_LandsInTheChildAdminAsTheLinkedUser()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
        var html = await response.Content.ReadAsStringAsync();

        // Assert: the browser went parent → child → parent → child, and ended on the child admin.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hops = browser.LastHops.Select(hop => $"{hop.Host}{hop.AbsolutePath}").ToList();
        Assert.Contains("business1.firma.localhost/delegated-access/enter", hops);
        Assert.Contains("firma.localhost/delegated-access/authorize", hops);
        Assert.Contains("business1.firma.localhost/delegated-access/callback", hops);
        Assert.Equal("business1.firma.localhost", browser.LastHops[browser.LastHops.Count - 1].Host);
        Assert.Contains("/Admin", browser.LastHops[browser.LastHops.Count - 1].AbsolutePath, StringComparison.OrdinalIgnoreCase);

        // The tenant switcher names the parent, and the page never names another child tenant.
        Assert.Contains("th-switcher", html, StringComparison.Ordinal);
        Assert.Contains("Firm A", html, StringComparison.Ordinal);
        Assert.Contains("http://firma.localhost/delegated-access/switch", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Business Two", html, StringComparison.Ordinal);
        Assert.DoesNotContain("business2", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Business Four", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenChild_TheChildAdminStaysReachableAfterwards()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");

        // Act
        var response = await browser.GetAsync($"{BusinessOneAddress}/Admin");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenChild_WithoutSigningIn_SendsTheUserToTheParentLogin()
    {
        // Arrange
        using var browser = _fixture.Host.CreateBrowser();

        // Act
        var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");

        // Assert
        Assert.Equal("firma.localhost", browser.LastHops[browser.LastHops.Count - 1].Host);
        Assert.Contains("/Login", browser.LastHops[browser.LastHops.Count - 1].AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenChild_OfAnotherParent_ShowsTheGenericError()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessFour.EntryId}");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("You cannot open this", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Business Four", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_FromAScript_IsRefused()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        var url = $"{TenantHierarchyFixture.FirmAAddress}/delegated-access/authorize?client_id={_fixture.BusinessOne.TenantId}&state=x&code_challenge={DelegatedAccessTokens.CreateCodeChallenge(DelegatedAccessTokens.CreateToken())}&code_challenge_method=S256";

        // Act
        var fromFetch = await browser.GetAsync(url, new Dictionary<string, string> { ["Sec-Fetch-Mode"] = "cors", ["Sec-Fetch-Site"] = "same-site" });
        var asNavigation = await browser.GetAsync(url, new Dictionary<string, string> { ["Sec-Fetch-Mode"] = "navigate", ["Sec-Fetch-Site"] = "same-site" });

        // Assert
        Assert.True(fromFetch.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.Redirect, asNavigation.StatusCode);
        Assert.StartsWith("http://business1.firma.localhost/delegated-access/callback", asNavigation.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParentPage_RequestedByAScriptOnAChildPage_IsRefused()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin", new Dictionary<string, string>
        {
            ["Sec-Fetch-Site"] = "same-site",
            ["Sec-Fetch-Mode"] = "cors",
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Open_WithAnExternalReturnUrl_DropsIt()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}?returnUrl=https%3A%2F%2Fevil.example%2F");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{BusinessOneAddress}/delegated-access/enter", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Callback_WithoutTheStateCookieOfThisBrowser_IsRefused()
    {
        // Arrange: a code meant for another browser, replayed in a fresh one.
        var verifier = DelegatedAccessTokens.CreateToken();
        var (_, callback) = await DelegatedAccessHelper.IssueCodeAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne.TenantId, verifier);
        using var attacker = _fixture.Host.CreateBrowser();

        // Act
        var response = await attacker.GetAsync(callback);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(attacker.HasCookie("business1.firma.localhost", "orchauth_"));
    }

    [Fact]
    public async Task LeaveChild_EndsTheSessionAndReturnsToTheParent()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
        var token = await browser.GetAntiforgeryTokenAsync($"{BusinessOneAddress}/Admin");

        // Act
        var leave = await browser.PostAsync($"{BusinessOneAddress}/delegated-access/sign-out", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        });
        var afterwards = await browser.GetAsync($"{BusinessOneAddress}/Admin");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, leave.StatusCode);
        Assert.Equal($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/home", leave.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, afterwards.StatusCode);
        Assert.Contains("Login", afterwards.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HostedPicker_ListsOnlyTheChildrenTheUserMayOpen()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/switch");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Business One", html, StringComparison.Ordinal);
        Assert.Contains("Business Two", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Business Four", html, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").First(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChildTenantsAdmin_RendersTheList()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Business One", html, StringComparison.Ordinal);
        Assert.Contains("business1.firma.localhost", html, StringComparison.Ordinal);
        Assert.Contains($"/delegated-access/open/{_fixture.BusinessOne.EntryId}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Business Four", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Admin/children/create")]
    [InlineData("/Admin/children/access")]
    [InlineData("/Admin/children/activity")]
    [InlineData("/Admin/children/switch")]
    public async Task ParentAdminPages_Render(string path)
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}{path}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChildAdminPages_ForOneChild_Render()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        var entryId = _fixture.BusinessOne.EntryId;

        // Act + Assert
        foreach (var path in new[] { $"/Admin/children/{entryId}/edit", $"/Admin/children/{entryId}/features", $"/Admin/children/{entryId}/access" })
        {
            var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}{path}");
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} returned {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task ChildAdminPage_WithAnotherParentsEntry_IsNotFound()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/{_fixture.BusinessFour.EntryId}/edit");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ParentUserWithoutPermission_CannotOpenTheChildTenantsAdmin()
    {
        // Arrange
        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Carol, _fixture.Password);

        // Act
        var response = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children");

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlatformPages_Render()
    {
        // Arrange
        using var browser = _fixture.Host.CreateBrowser();
        var platform = "http://localhost";
        await browser.SignInAsync(platform, "platform", _fixture.Password);

        // Act + Assert
        foreach (var path in new[] { "/Admin/tenant-hierarchy", "/Admin/tenant-hierarchy/make-parent", $"/Admin/tenant-hierarchy/parents/{TenantHierarchyFixture.FirmA}", $"/Admin/tenant-hierarchy/parents/{TenantHierarchyFixture.FirmA}/policy" })
        {
            var response = await browser.NavigateAsync($"{platform}{path}");
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} returned {(int)response.StatusCode}.");
        }
    }

    private async Task<TestBrowser> SignInAsAliceAsync()
    {
        var browser = _fixture.Host.CreateBrowser();
        var response = await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Alice, _fixture.Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(browser.HasCookie("firma.localhost", "orchauth_"), "The parent sign-in did not set its cookie.");

        return browser;
    }
}
