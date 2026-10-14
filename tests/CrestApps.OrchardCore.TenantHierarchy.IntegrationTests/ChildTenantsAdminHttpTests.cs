using System.Net;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Drives every action of the child tenants admin of a parent over HTTP, as its administrator and as a user who may
/// only view the list. The tests use a parent of their own, so the shared parents keep their children.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class ChildTenantsAdminHttpTests
{
    private const string Owner = "owner";
    private const string Viewer = "viewer";

    private static readonly SemaphoreSlim _parentLock = new(1, 1);
    private static string _parent;

    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChildTenantsAdminHttpTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public ChildTenantsAdminHttpTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("")]
    [InlineData("?Options.State=Running&Options.OrderBy=Newest")]
    [InlineData("?Options.State=Suspended&Options.OrderBy=State")]
    [InlineData("?Options.State=SettingUp")]
    [InlineData("?Options.State=Failed")]
    [InlineData("?Options.State=PendingRemoval")]
    [InlineData("?Options.State=ChangedByPlatform")]
    [InlineData("?Options.Search=nothing-matches-this")]
    public async Task List_WithEveryFilterAndSort_Renders(string query)
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;

        // Act
        var response = await browser.NavigateAsync($"{Address(parent)}/Admin/children{query}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FilterForm_RedirectsToTheFilteredList_ThatShowsOnlyMatches()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var match = await _fixture.CreateChildAsync(parent, "Filter Match", NewSlug("match"));
        await _fixture.CreateChildAsync(parent, "Filter Other", NewSlug("other"));
        var token = await browser.GetAntiforgeryTokenAsync($"{Address(parent)}/Admin/children");

        // Act
        var post = await browser.PostAsync($"{Address(parent)}/Admin/children", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["submit.Filter"] = "Filter",
            ["Options.Search"] = "Filter Match",
            ["Options.State"] = "Running",
            ["Options.OrderBy"] = "Newest",
        });
        var list = await browser.NavigateAsync($"{Address(parent)}{post.Headers.Location}");
        var html = await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains(match.DisplayName, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter Other", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WithAnInvalidAddress_ShowsTheErrorAndCreatesNothing()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var token = await browser.GetAntiforgeryTokenAsync($"{Address(parent)}/Admin/children/create");
        var before = await CountChildrenAsync(parent);

        // Act
        var response = await browser.PostAsync($"{Address(parent)}/Admin/children/create", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DisplayName"] = "Bad Address",
            ["Slug"] = "admin",
            ["RecipeName"] = "Blank",
        });
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", html, StringComparison.Ordinal);
        Assert.Equal(before, await CountChildrenAsync(parent));
    }

    [Fact]
    public async Task Create_WithValidValues_CreatesTheChildAndSetsItUp()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var token = await browser.GetAntiforgeryTokenAsync($"{Address(parent)}/Admin/children/create");
        var slug = NewSlug("created");

        // Act
        var response = await browser.PostAsync($"{Address(parent)}/Admin/children/create", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DisplayName"] = "Created Over HTTP",
            ["Slug"] = slug,
            ["Description"] = "Made by a test.",
            ["RecipeName"] = "Blank",
        });

        // Assert: the setup runs after the response, so wait for it.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var entry = await FindBySlugAsync(parent, slug);
        Assert.NotNull(entry);
        Assert.True(await WaitUntilAsync(() => _fixture.Host.GetSettings(entry.TenantName)?.IsRunning() == true), "The child was not set up.");
        Assert.Equal("Made by a test.", (await FindBySlugAsync(parent, slug)).Description);
    }

    [Fact]
    public async Task Edit_WithAnInvalidAddress_ShowsTheError_AndWithValidValues_MovesTheChild()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var entry = await _fixture.CreateChildAsync(parent, "Edit Me", NewSlug("edit"));
        var url = $"{Address(parent)}/Admin/children/{entry.EntryId}/edit";
        var token = await browser.GetAntiforgeryTokenAsync(url);
        var newSlug = NewSlug("edited");

        // Act
        var invalid = await browser.PostAsync(url, new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DisplayName"] = "Edit Me",
            ["Slug"] = "www",
        });
        var valid = await browser.PostAsync(url, new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DisplayName"] = "Edited",
            ["Slug"] = newSlug,
            ["Description"] = "Renamed",
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("field-validation-error", await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        Assert.Equal($"{newSlug}.{parent}.localhost", _fixture.Host.GetSettings(entry.TenantName).RequestUrlHost);
        Assert.Equal("Edited", (await FindBySlugAsync(parent, newSlug)).DisplayName);
    }

    [Fact]
    public async Task SuspendResumeReload_OverHttp_ChangeTheChild()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var entry = await _fixture.CreateChildAsync(parent, "Life Cycle", NewSlug("life"));
        var fields = await TokenAsync(browser, parent);

        // Act + Assert
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/suspend", fields)).StatusCode);
        Assert.True(_fixture.Host.GetSettings(entry.TenantName).IsDisabled());

        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/resume", fields)).StatusCode);
        Assert.True(_fixture.Host.GetSettings(entry.TenantName).IsRunning());

        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/reload", fields)).StatusCode);
        Assert.True(_fixture.Host.GetSettings(entry.TenantName).IsRunning());

        // An entry of another parent is not found and changes nothing.
        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync($"{Address(parent)}/Admin/children/{_fixture.BusinessFour.EntryId}/suspend", fields)).StatusCode);
        Assert.True(_fixture.Host.GetSettings(_fixture.BusinessFour.TenantName).IsRunning());
    }

    [Fact]
    public async Task BulkActions_SuspendAndResumeEverySelectedChild()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var first = await _fixture.CreateChildAsync(parent, "Bulk One", NewSlug("bulka"));
        var second = await _fixture.CreateChildAsync(parent, "Bulk Two", NewSlug("bulkb"));
        var token = await browser.GetAntiforgeryTokenAsync($"{Address(parent)}/Admin/children");

        Task<HttpResponseMessage> PostBulkAsync(string action) => browser.PostAsync($"{Address(parent)}/Admin/children", new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("submit.BulkAction", "BulkAction"),
            new("Options.BulkAction", action),
            new("itemIds", first.EntryId),
            new("itemIds", second.EntryId),
        });

        // Act + Assert
        Assert.Equal(HttpStatusCode.Redirect, (await PostBulkAsync("Suspend")).StatusCode);
        Assert.True(_fixture.Host.GetSettings(first.TenantName).IsDisabled());
        Assert.True(_fixture.Host.GetSettings(second.TenantName).IsDisabled());

        Assert.Equal(HttpStatusCode.Redirect, (await PostBulkAsync("Resume")).StatusCode);
        Assert.True(_fixture.Host.GetSettings(first.TenantName).IsRunning());
        Assert.True(_fixture.Host.GetSettings(second.TenantName).IsRunning());

        Assert.Equal(HttpStatusCode.Redirect, (await PostBulkAsync("Reload")).StatusCode);
        Assert.True(_fixture.Host.GetSettings(first.TenantName).IsRunning());
    }

    [Fact]
    public async Task BulkRemove_RemovesOnlyTheSuspendedChildren()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var suspended = await _fixture.CreateChildAsync(parent, "Bulk Suspended", NewSlug("bulks"));
        var running = await _fixture.CreateChildAsync(parent, "Bulk Running", NewSlug("bulkr"));
        await InParentAsync(parent, manager => manager.SuspendAsync(suspended.EntryId));
        var token = await browser.GetAntiforgeryTokenAsync($"{Address(parent)}/Admin/children");

        // Act
        var response = await browser.PostAsync($"{Address(parent)}/Admin/children", new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("submit.BulkAction", "BulkAction"),
            new("Options.BulkAction", "Remove"),
            new("itemIds", suspended.EntryId),
            new("itemIds", running.EntryId),
        });

        // Assert: a running child cannot be removed, and it says so instead of failing the whole action.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(_fixture.Host.GetSettings(suspended.TenantName));
        Assert.True(_fixture.Host.GetSettings(running.TenantName).IsRunning());
    }

    [Fact]
    public async Task Remove_WithTheWrongName_IsRefused_AndWithTheRightName_SchedulesItAndRestoreBringsItBack()
    {
        // Arrange: keep removed children for a week, so the removal can be restored.
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        await UpdatePolicyAsync(parent, policy => policy.RemovalGraceDays = 7);
        var entry = await _fixture.CreateChildAsync(parent, "Remove Me", NewSlug("remove"));
        await InParentAsync(parent, manager => manager.SuspendAsync(entry.EntryId));
        var url = $"{Address(parent)}/Admin/children/{entry.EntryId}/remove";
        var token = await browser.GetAntiforgeryTokenAsync(url);

        try
        {
            // Act
            var wrong = await browser.PostAsync(url, new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["ConfirmName"] = "Remove" });
            var right = await browser.PostAsync(url, new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["ConfirmName"] = "Remove Me" });
            var scheduled = await FindBySlugAsync(parent, entry.Slug);
            var restore = await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/restore", new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

            // Assert
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
            Assert.Contains("field-validation-error", await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
            Assert.Equal(ChildTenantStatus.PendingRemoval, scheduled.Status);
            Assert.Equal(HttpStatusCode.Redirect, restore.StatusCode);
            Assert.Equal(ChildTenantStatus.Ready, (await FindBySlugAsync(parent, entry.Slug)).Status);
            Assert.NotNull(_fixture.Host.GetSettings(entry.TenantName));
        }
        finally
        {
            await UpdatePolicyAsync(parent, policy => policy.RemovalGraceDays = 0);
        }
    }

    [Fact]
    public async Task MaintenanceTask_RemovesAChildWhoseGracePeriodEnded()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        browser.Dispose();
        await UpdatePolicyAsync(parent, policy => policy.RemovalGraceDays = 7);
        var entry = await _fixture.CreateChildAsync(parent, "Due Child", NewSlug("due"));

        try
        {
            await InParentAsync(parent, manager => manager.SuspendAsync(entry.EntryId));
            await InParentAsync(parent, manager => manager.RemoveAsync(entry.EntryId));
            await _fixture.Host.InTenantAsync(parent, async services =>
            {
                var store = services.GetRequiredService<ChildTenantEntryStore>();
                var due = await store.FindByEntryIdAsync(entry.EntryId);
                due.RetainUntilUtc = DateTime.UtcNow.AddMinutes(-1);
                await store.SaveAsync(due);
            });

            // Act: what the background task does every five minutes.
            await _fixture.Host.InTenantAsync(parent, services =>
                new BackgroundTasks.ChildTenantMaintenanceBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken));

            // Assert
            Assert.Null(_fixture.Host.GetSettings(entry.TenantName));
            Assert.Null(await FindBySlugAsync(parent, entry.Slug));
        }
        finally
        {
            await UpdatePolicyAsync(parent, policy => policy.RemovalGraceDays = 0);
        }
    }

    [Fact]
    public async Task MaintenanceTask_InATenantThatIsNotAParent_DoesNothing()
    {
        // Act + Assert: the task runs in every tenant; outside a parent it must not touch anything.
        await _fixture.Host.InTenantAsync(TenantHierarchyFixture.Plain, services =>
            new BackgroundTasks.ChildTenantMaintenanceBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RetryAndDiscard_WorkOnlyOnAChildWhoseSetupFailed()
    {
        // Arrange: two children whose setup "failed", and one that is running.
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var retried = await CreateFailedChildAsync(parent, "Retry Me");
        var discarded = await CreateFailedChildAsync(parent, "Discard Me");
        var running = await _fixture.CreateChildAsync(parent, "Not Failed", NewSlug("notfailed"));
        var fields = await TokenAsync(browser, parent);

        // Act
        var retry = await browser.PostAsync($"{Address(parent)}/Admin/children/{retried.EntryId}/retry", fields);
        var discard = await browser.PostAsync($"{Address(parent)}/Admin/children/{discarded.EntryId}/discard", fields);
        var discardRunning = await browser.PostAsync($"{Address(parent)}/Admin/children/{running.EntryId}/discard", fields);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        Assert.True(await WaitUntilAsync(async () => (await FindBySlugAsync(parent, retried.Slug))?.Status == ChildTenantStatus.Ready), "The retried child was not set up.");
        Assert.Equal(HttpStatusCode.Redirect, discard.StatusCode);
        Assert.Null(await FindBySlugAsync(parent, discarded.Slug));
        Assert.Equal(HttpStatusCode.Redirect, discardRunning.StatusCode);
        Assert.NotNull(await FindBySlugAsync(parent, running.Slug));
    }

    [Fact]
    public async Task Dismiss_RemovesTheEntryOfAChildThePlatformDetached()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var entry = await _fixture.CreateChildAsync(parent, "Taken Away", NewSlug("taken"));
        await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
            Assert.True((await services.GetRequiredService<TenantHierarchyPlatformService>().DetachChildAsync(entry.TenantName)).Succeeded));
        var fields = await TokenAsync(browser, parent);

        // Act
        var response = await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/dismiss", fields);

        // Assert: the entry goes; the tenant, now an ordinary one, stays.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await FindBySlugAsync(parent, entry.Slug));
        Assert.NotNull(_fixture.Host.GetSettings(entry.TenantName));
    }

    [Fact]
    public async Task Features_EnableThenDisable_ChangesTheChildsFeatures()
    {
        // Arrange
        var (parent, browser) = await SignInAsOwnerAsync();
        using var _ = browser;
        var entry = await _fixture.CreateChildAsync(parent, "Feature Child", NewSlug("feature"));
        var url = $"{Address(parent)}/Admin/children/{entry.EntryId}/features";
        var token = await browser.GetAntiforgeryTokenAsync(url);
        const string Feature = "OrchardCore.Apis.GraphQL";

        // Act
        var enable = await browser.PostAsync(url, new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["featureIds"] = Feature, ["enable"] = "true" });
        var enabled = await IsFeatureEnabledAsync(entry.TenantName, Feature);
        var disable = await browser.PostAsync(url, new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["featureIds"] = Feature, ["enable"] = "false" });
        var disabled = !await IsFeatureEnabledAsync(entry.TenantName, Feature);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, enable.StatusCode);
        Assert.True(enabled);
        Assert.Equal(HttpStatusCode.Redirect, disable.StatusCode);
        Assert.True(disabled);
    }

    [Fact]
    public async Task UserWhoMayOnlyView_SeesTheList_ButCannotChangeAnything()
    {
        // Arrange
        var parent = await EnsureParentAsync();
        var entry = await _fixture.CreateChildAsync(parent, "Viewer Target", NewSlug("viewer"));
        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(Address(parent), Viewer, _fixture.Password);
        var token = await browser.GetAntiforgeryTokenAsync($"{Address(parent)}/Admin/children");
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };

        // Act
        var list = await browser.GetAsync($"{Address(parent)}/Admin/children");
        var refused = new Dictionary<string, HttpResponseMessage>
        {
            ["create page"] = await browser.GetAsync($"{Address(parent)}/Admin/children/create"),
            ["create"] = await browser.PostAsync($"{Address(parent)}/Admin/children/create", new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["DisplayName"] = "Nope", ["Slug"] = NewSlug("nope"), ["RecipeName"] = "Blank" }),
            ["suspend"] = await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/suspend", fields),
            ["features"] = await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/features", new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["featureIds"] = "OrchardCore.Apis.GraphQL", ["enable"] = "true" }),
            ["remove"] = await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/remove", new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["ConfirmName"] = "Viewer Target" }),
            ["access"] = await browser.GetAsync($"{Address(parent)}/Admin/children/access"),
            ["edit"] = await browser.PostAsync($"{Address(parent)}/Admin/children/{entry.EntryId}/edit", new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["DisplayName"] = "Changed", ["Slug"] = entry.Slug }),
        };

        // Assert
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        foreach (var (action, response) in refused)
        {
            // Orchard Core answers a refused request with 403, or sends the browser to its 403 page.
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden ||
                response.StatusCode == HttpStatusCode.Redirect && location.Contains("/Error/403", StringComparison.Ordinal),
                $"The viewer could {action}: {(int)response.StatusCode} {location}");
        }

        Assert.True(_fixture.Host.GetSettings(entry.TenantName).IsRunning());
        Assert.Equal("Viewer Target", (await FindBySlugAsync(parent, entry.Slug)).DisplayName);
        Assert.False(await IsFeatureEnabledAsync(entry.TenantName, "OrchardCore.Apis.GraphQL"));
        Assert.Null(await FindBySlugAsync(parent, "nope"));
    }

    private async Task<(string Parent, TestBrowser Browser)> SignInAsOwnerAsync()
    {
        var parent = await EnsureParentAsync();
        var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(Address(parent), Owner, _fixture.Password);

        return (parent, browser);
    }

    private async Task<string> EnsureParentAsync()
    {
        await _parentLock.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            if (_parent is not null)
            {
                return _parent;
            }

            var name = "adminhttp";
            await _fixture.Host.CreateTenantAsync(name, $"{name}.localhost");
            await _fixture.Host.SetupTenantAsync(name, "Blank", Owner, _fixture.Password);
            await _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
            {
                var (made, errors) = await services.GetRequiredService<TenantHierarchyPlatformService>().MakeParentAsync(name, name, "Admin Http", new ParentTenantPolicy());
                Assert.True(made.Succeeded, $"{made.Error} {string.Join(' ', errors.Values)}");
            });

            // A user whose only permissions are to reach the admin and view the child tenants.
            await _fixture.Host.InTenantAsync(name, async services =>
            {
                var roleManager = services.GetRequiredService<RoleManager<IRole>>();
                var role = new Role { RoleName = "Child Tenant Viewer" };
                role.RoleClaims.Add(new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = "AccessAdminPanel" });
                role.RoleClaims.Add(new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = TenantHierarchyPermissions.ViewChildTenants.Name });
                Assert.True((await roleManager.CreateAsync(role)).Succeeded);
            });

            // The role document is saved when its scope ends, so the user is added in a scope of its own.
            await _fixture.Host.InTenantAsync(name, async services =>
            {
                var userManager = services.GetRequiredService<UserManager<IUser>>();
                var user = new User { UserName = Viewer, Email = $"{Viewer}@example.invalid", EmailConfirmed = true, IsEnabled = true };
                Assert.True((await userManager.CreateAsync(user, _fixture.Password)).Succeeded);
                Assert.True((await userManager.AddToRoleAsync(user, "Child Tenant Viewer")).Succeeded);
            });

            return _parent = name;
        }
        finally
        {
            _parentLock.Release();
        }
    }

    private async Task<ChildTenantEntry> CreateFailedChildAsync(string parent, string displayName)
    {
        return await _fixture.Host.InTenantAsync(parent, async services =>
        {
            var (result, entry) = await services.GetRequiredService<ChildTenantManager>().CreateAsync(new CreateChildTenantRequest
            {
                DisplayName = displayName,
                Slug = NewSlug("failed"),
                RecipeName = "Blank",
            });
            Assert.True(result.Succeeded, result.Error);

            // What a setup that threw leaves behind.
            var store = services.GetRequiredService<ChildTenantEntryStore>();
            var failed = await store.FindByEntryIdAsync(entry.EntryId);
            failed.Status = ChildTenantStatus.Failed;
            failed.Error = "The setup failed.";
            await store.SaveAsync(failed);

            return failed;
        });
    }

    private Task<TenantHierarchyResult> InParentAsync(string parent, Func<ChildTenantManager, Task<TenantHierarchyResult>> operation)
        => _fixture.Host.InTenantAsync(parent, services => operation(services.GetRequiredService<ChildTenantManager>()));

    private Task<ChildTenantEntry> FindBySlugAsync(string parent, string slug)
        => _fixture.Host.InTenantAsync(parent, async services =>
            (await services.GetRequiredService<ChildTenantEntryStore>().ListAsync()).FirstOrDefault(entry => entry.Slug == slug));

    private Task<int> CountChildrenAsync(string parent)
        => _fixture.Host.InTenantAsync(parent, async services => (await services.GetRequiredService<ChildTenantEntryStore>().ListAsync()).Count);

    private Task<bool> IsFeatureEnabledAsync(string tenant, string featureId)
        => _fixture.Host.InTenantAsync(tenant, async services =>
            (await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync()).Any(feature => feature.Id == featureId));

    private Task UpdatePolicyAsync(string parent, Action<ParentTenantPolicy> change)
        => _fixture.Host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var settings = _fixture.Host.GetSettings(parent);
            var policy = settings.GetParentPolicy();
            change(policy);
            var (result, _) = await services.GetRequiredService<TenantHierarchyPlatformService>().UpdateParentAsync(parent, settings.GetHierarchyDisplayName(), policy);
            Assert.True(result.Succeeded, result.Error);
        });

    private static async Task<Dictionary<string, string>> TokenAsync(TestBrowser browser, string parent)
        => new() { ["__RequestVerificationToken"] = await browser.GetAntiforgeryTokenAsync($"{Address(parent)}/Admin/children") };

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
        => await WaitUntilAsync(() => Task.FromResult(condition()));

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(250, TestContext.Current.CancellationToken);
        }

        return false;
    }

    private static string Address(string parent) => $"http://{parent}.localhost";

    private static string NewSlug(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 8, 40)];
}
