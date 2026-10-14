using System.Net;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Drives the platform screens, the access rules and the tenant picker endpoints over HTTP. Every test that changes a
/// parent uses tenants of its own, so the shared hierarchy stays as the other tests expect it.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class PlatformAndAccessHttpTests
{
    private const string Platform = "http://localhost";

    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformAndAccessHttpTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public PlatformAndAccessHttpTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MakeParent_WithAnInvalidAddress_ShowsTheError_AndWithValidValues_MakesTheParent()
    {
        // Arrange
        var tenant = await CreatePlainTenantAsync("mkp");
        using var browser = await SignInToPlatformAsync();
        var url = $"{Platform}/Admin/tenant-hierarchy/make-parent";
        var token = await browser.GetAntiforgeryTokenAsync(url);

        // Act
        var invalid = await browser.PostAsync(url, PolicyFields(token, tenant, slug: "www", displayName: "Invalid"));
        var valid = await browser.PostAsync(url, PolicyFields(token, tenant, slug: tenant, displayName: "Made Over Http"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("field-validation-error", await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        Assert.Contains($"/Admin/tenant-hierarchy/parents/{tenant}", valid.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.True(_fixture.Host.GetSettings(tenant).IsParentTenant());
        Assert.Equal("Made Over Http", _fixture.Host.GetSettings(tenant).GetHierarchyDisplayName());
    }

    [Fact]
    public async Task EditPolicy_WithAnInvalidLimit_ShowsTheError_AndWithValidValues_SavesThePolicy()
    {
        // Arrange
        var parent = await CreateParentAsync("pol");
        using var browser = await SignInToPlatformAsync();
        var url = $"{Platform}/Admin/tenant-hierarchy/parents/{parent}/policy";
        var token = await browser.GetAntiforgeryTokenAsync(url);

        // Act
        var invalid = await browser.PostAsync(url, PolicyFields(token, parent, slug: parent, displayName: "Policy Parent", maxChildren: "-1"));
        var valid = await browser.PostAsync(url, PolicyFields(token, parent, slug: parent, displayName: "Policy Parent", maxChildren: "7", childLabel: "Client"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("field-validation-error", await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        var policy = _fixture.Host.GetSettings(parent).GetParentPolicy();
        Assert.Equal(7, policy.MaxChildren);
        Assert.Equal("Client", policy.Labels.Child);
    }

    [Fact]
    public async Task SuspendResumeAndRemove_OverHttp_ApplyToTheParentAndItsChildren()
    {
        // Arrange
        var parent = await CreateParentAsync("srr");
        var child = await _fixture.CreateChildAsync(parent, "Platform Child", "pchild");
        using var browser = await SignInToPlatformAsync();
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = await browser.GetAntiforgeryTokenAsync($"{Platform}/Admin/tenant-hierarchy") };

        // Act + Assert: suspend and resume take the children along.
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/parents/{parent}/suspend", fields)).StatusCode);
        Assert.True(_fixture.Host.GetSettings(parent).IsDisabled());
        Assert.True(_fixture.Host.GetSettings(child.TenantName).IsDisabled());

        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/parents/{parent}/resume", fields)).StatusCode);
        Assert.True(_fixture.Host.GetSettings(parent).IsRunning());
        Assert.True(_fixture.Host.GetSettings(child.TenantName).IsRunning());

        // Removing needs the exact tenant name, and a suspended parent.
        await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/parents/{parent}/suspend", fields);
        var wrongName = await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/parents/{parent}/remove", new Dictionary<string, string>(fields) { ["confirmName"] = "wrong" });
        Assert.Equal(HttpStatusCode.Redirect, wrongName.StatusCode);
        Assert.NotNull(_fixture.Host.GetSettings(parent));

        var removed = await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/parents/{parent}/remove", new Dictionary<string, string>(fields) { ["confirmName"] = parent });
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Null(_fixture.Host.GetSettings(parent));
        Assert.Null(_fixture.Host.GetSettings(child.TenantName));
    }

    [Fact]
    public async Task Move_OverHttp_MovesTheChildToAnotherParent()
    {
        // Arrange
        var from = await CreateParentAsync("mvf");
        var to = await CreateParentAsync("mvt");
        var child = await _fixture.CreateChildAsync(from, "Moving Child", "moving");
        using var browser = await SignInToPlatformAsync();
        var token = await browser.GetAntiforgeryTokenAsync($"{Platform}/Admin/tenant-hierarchy/parents/{from}");

        // Act
        var response = await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/parents/{from}/move", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["child"] = child.TenantName,
            ["newParent"] = to,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(_fixture.Host.GetSettings(child.TenantName).IsChildOf(_fixture.Host.GetSettings(to)));
    }

    [Fact]
    public async Task AdoptAndDetach_OverHttp_HandleOrphanedChildren()
    {
        // Arrange: two children whose parent the settings no longer name, as after a parent was removed by hand.
        var parent = await CreateParentAsync("orp");
        var adopted = await _fixture.CreateChildAsync(parent, "Adopted Child", "adopted");
        var detached = await _fixture.CreateChildAsync(parent, "Detached Orphan", "orphan");
        await OrphanAsync(adopted.TenantName);
        await OrphanAsync(detached.TenantName);
        var newParent = await CreateParentAsync("adp");
        using var browser = await SignInToPlatformAsync();
        var index = await browser.NavigateAsync($"{Platform}/Admin/tenant-hierarchy");
        var html = await index.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = await browser.GetAntiforgeryTokenAsync($"{Platform}/Admin/tenant-hierarchy");

        // Act
        var adopt = await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/orphans/adopt", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["child"] = adopted.TenantName,
            ["newParent"] = newParent,
        });
        var detach = await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/orphans/detach", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["child"] = detached.TenantName,
        });

        // Assert
        Assert.Contains("orphaned child tenant", html, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, adopt.StatusCode);
        Assert.True(_fixture.Host.GetSettings(adopted.TenantName).IsChildOf(_fixture.Host.GetSettings(newParent)));
        Assert.Equal(HttpStatusCode.Redirect, detach.StatusCode);
        Assert.False(_fixture.Host.GetSettings(detached.TenantName).IsInTenantHierarchy());
    }

    [Fact]
    public async Task Unmake_OverHttp_MakesAParentWithoutChildrenOrdinary()
    {
        // Arrange
        var parent = await CreateParentAsync("unm");
        using var browser = await SignInToPlatformAsync();
        var token = await browser.GetAntiforgeryTokenAsync($"{Platform}/Admin/tenant-hierarchy/parents/{parent}");

        // Act
        var response = await browser.PostAsync($"{Platform}/Admin/tenant-hierarchy/parents/{parent}/unmake", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(_fixture.Host.GetSettings(parent).IsInTenantHierarchy());
    }

    [Fact]
    public async Task PlatformScreens_FromAParent_AreRefused()
    {
        // Arrange
        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Alice, _fixture.Password);

        // Act
        var response = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/tenant-hierarchy");

        // Assert: the platform screens exist only in the Default tenant.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AccessRules_AddForARoleAndForOnePerson_ThenRemove()
    {
        // Arrange
        var parent = await CreateParentAsync("acc", ownerName: "owner");
        var child = await _fixture.CreateChildAsync(parent, "Access Child", "access");
        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync($"http://{parent}.localhost", "owner", _fixture.Password);
        var url = $"http://{parent}.localhost/Admin/children/{child.EntryId}/access";
        var token = await browser.GetAntiforgeryTokenAsync(url);

        // Act
        var noRoles = await browser.PostAsync($"http://{parent}.localhost/Admin/children/access/add?id={child.EntryId}", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["PrincipalType"] = "Role",
            ["PrincipalRole"] = "Administrator",
        });
        var byRole = await browser.PostAsync($"http://{parent}.localhost/Admin/children/access/add?id={child.EntryId}", new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("PrincipalType", "Role"),
            new("PrincipalRole", "Administrator"),
            new("SelectedChildRoles", "Editor"),
        });
        var byPerson = await browser.PostAsync($"http://{parent}.localhost/Admin/children/access/add", new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("PrincipalType", "User"),
            new("Principal", "owner"),
            new("SelectedChildRoles", "Author"),
        });
        var unknownPerson = await browser.PostAsync($"http://{parent}.localhost/Admin/children/access/add", new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("PrincipalType", "User"),
            new("Principal", "nobody-by-this-name"),
            new("SelectedChildRoles", "Author"),
        });
        var grants = await ListGrantsAsync(parent, child.EntryId);
        var page = await (await browser.GetAsync(url)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var added = grants.Single(grant => grant.ChildEntryId == child.EntryId);
        var remove = await browser.PostAsync($"http://{parent}.localhost/Admin/children/access/{added.GrantId}/remove?id={child.EntryId}", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, noRoles.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, byRole.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, byPerson.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, unknownPerson.StatusCode);
        Assert.Equal(["Editor"], added.ChildRoles);
        Assert.Contains(grants, grant => grant.PrincipalType == AccessGrantPrincipalType.User && grant.PrincipalName == "owner" && grant.ChildEntryId is null);
        Assert.DoesNotContain(grants, grant => grant.PrincipalName == "nobody-by-this-name");
        Assert.Contains("Roles inside the", page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);
        Assert.DoesNotContain(await ListGrantsAsync(parent, child.EntryId), grant => grant.GrantId == added.GrantId);
    }

    [Fact]
    public async Task HomeAndSwitch_SendTheUserToTheChildTenantsAdminAndThePicker()
    {
        // Arrange
        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Alice, _fixture.Password);

        // Act
        var home = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/home");
        var switcher = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/switch");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.EndsWith("/Admin/children", home.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, switcher.StatusCode);
        Assert.EndsWith("/Admin/children/switch", switcher.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmbeddedPicker_IsFramedOnlyByTheChildThatAsked()
    {
        // Arrange: a parent whose switcher shows the picker in a panel.
        var parent = await CreateParentAsync("emb", ownerName: "owner", policy: new ParentTenantPolicy { SwitcherMode = SwitcherMode.Embedded });
        var child = await _fixture.CreateChildAsync(parent, "Framing Child", "framing");
        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync($"http://{parent}.localhost", "owner", _fixture.Password);

        // Act
        var framed = await browser.GetAsync($"http://{parent}.localhost/{TenantHierarchyConstants.Routes.EmbeddedPicker}?child={child.TenantId}");
        var unknown = await browser.GetAsync($"http://{parent}.localhost/{TenantHierarchyConstants.Routes.EmbeddedPicker}?child=not-a-child");
        using var alice = _fixture.Host.CreateBrowser();
        await alice.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Alice, _fixture.Password);
        var hostedParent = await alice.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/{TenantHierarchyConstants.Routes.EmbeddedPicker}?child={_fixture.BusinessOne.TenantId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, framed.StatusCode);
        Assert.Equal($"frame-ancestors http://framing.{parent}.localhost", framed.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("Framing Child", await framed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("frame-ancestors 'none'", unknown.Headers.GetValues("Content-Security-Policy").Single());
        // A parent whose switcher opens its own picker page serves no framed picker.
        Assert.Equal(HttpStatusCode.NotFound, hostedParent.StatusCode);
    }

    [Fact]
    public async Task AuditTrailEventPage_ShowsTheHierarchyDetails()
    {
        // Arrange: opening a child records an event with the roles, the sign-in method and the session.
        await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);
        var opened = (await AuditTrailHelper.GetEventsAsync(_fixture.Host, TenantHierarchyFixture.FirmA, _fixture.BusinessOne.EntryId))
            .First(item => item.Event.Name == HierarchyAuditEventNames.Entered);
        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Alice, _fixture.Password);

        // Act
        var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/AuditTrail/Display/{opened.Event.EventId}");
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Child tenant opened", html, StringComparison.Ordinal);
        Assert.Contains("Business One", html, StringComparison.Ordinal);
        Assert.Contains("Roles: Administrator", html, StringComparison.Ordinal);
        Assert.Contains(opened.Data.SessionReference, html, StringComparison.Ordinal);
    }

    private async Task<TestBrowser> SignInToPlatformAsync()
    {
        var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(Platform, "platform", _fixture.Password);

        return browser;
    }

    private async Task<string> CreatePlainTenantAsync(string prefix, string ownerName = "owner")
    {
        var name = $"{prefix}{Guid.NewGuid():N}"[..(prefix.Length + 6)];
        await _fixture.Host.CreateTenantAsync(name, $"{name}.localhost");
        await _fixture.Host.SetupTenantAsync(name, "Blank", ownerName, _fixture.Password);

        return name;
    }

    private async Task<string> CreateParentAsync(string prefix, string ownerName = "owner", ParentTenantPolicy policy = null)
    {
        var name = await CreatePlainTenantAsync(prefix, ownerName);
        await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var (made, errors) = await services.GetRequiredService<TenantHierarchyPlatformService>().MakeParentAsync(name, name, $"Parent {name}", policy ?? new ParentTenantPolicy());
            Assert.True(made.Succeeded, $"{made.Error} {string.Join(' ', errors.Values)}");
        });

        return name;
    }

    private Task OrphanAsync(string childName)
        => _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var settings = _fixture.Host.GetSettings(childName);
            settings[TenantHierarchyConstants.SettingsKeys.ParentTenantId] = "a-parent-that-is-gone";
            await services.GetRequiredService<IShellHost>().UpdateShellSettingsAsync(settings);
        });

    private Task<List<AccessGrant>> ListGrantsAsync(string parent, string childEntryId)
        => _fixture.Host.InTenantAsync(parent, async services =>
        {
            var store = services.GetRequiredService<AccessGrantStore>();

            return (await store.ListParentWideAsync()).Concat(await store.ListForChildAsync(childEntryId)).ToList();
        });

    private static Dictionary<string, string> PolicyFields(
        string token,
        string tenant,
        string slug,
        string displayName,
        string maxChildren = "25",
        string childLabel = "")
        => new()
        {
            ["__RequestVerificationToken"] = token,
            ["TenantName"] = tenant,
            ["Slug"] = slug,
            ["DisplayName"] = displayName,
            ["MaxChildren"] = maxChildren,
            ["DatabaseStrategy"] = nameof(ChildDatabaseStrategy.SqlitePerChild),
            ["SessionValidationMinutes"] = "2",
            ["SessionIdleMinutes"] = "30",
            ["SessionLifetimeHours"] = "8",
            ["SwitcherMode"] = nameof(SwitcherMode.Hosted),
            ["RemovalGraceDays"] = "0",
            ["ChildLabel"] = childLabel,
        };
}
