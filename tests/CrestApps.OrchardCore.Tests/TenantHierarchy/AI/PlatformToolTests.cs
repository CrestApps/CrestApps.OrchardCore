using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.AI;
using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Recipes.Models;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy.AI;

/// <summary>
/// The platform tools run on the real <see cref="TenantHierarchyPlatformService"/> over a shell host double: they merge
/// a policy change into the stored policy, never return a tenant's connection string or secret, and keep the
/// confirmations and limits of the Tenant Hierarchy pages.
/// </summary>
public sealed class PlatformToolTests
{
    private delegate bool TryGetSettingsCallback(string name, out ShellSettings settings);

    private readonly List<ShellSettings> _tenants = [];
    private readonly List<ShellSettings> _updated = [];
    private readonly Mock<IShellRemovalManager> _removalManager = new();

    [Fact]
    public async Task GetTenantHierarchyOverview_ShowsTheTree_WithoutAnyConnectionStringOrSecret()
    {
        // Arrange
        var parent = AddParent("firm", new ParentTenantPolicy { MaxChildren = 50 });
        parent["ConnectionString"] = "Server=db;Password=hunter2";
        parent["Secret"] = "tenant-secret-canary";
        var child = AddChild("north", parent);
        child["ConnectionString"] = "Data Source=child-canary.db";
        var orphan = TestShellSettings.Child("lost", TestShellSettings.Parent("gone"));
        _tenants.Add(orphan);
        _tenants.Add(TestShellSettings.Ordinary("shop"));
        var services = Build();

        // Act
        var result = await InvokeAsync(new GetTenantHierarchyOverviewTool(), services, []);
        var detail = await InvokeAsync(new GetTenantHierarchyOverviewTool(), services, new() { ["parent"] = "firm" });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        var parentNode = Assert.Single(json.GetProperty("parents").EnumerateArray());

        Assert.Equal("firm", parentNode.GetProperty("tenant").GetProperty("tenantName").GetString());
        Assert.Equal(1, parentNode.GetProperty("childCount").GetInt32());
        Assert.Equal(50, parentNode.GetProperty("maxChildren").GetInt32());
        Assert.Equal("lost", Assert.Single(json.GetProperty("orphans").EnumerateArray()).GetProperty("tenantName").GetString());
        Assert.Equal("shop", Assert.Single(json.GetProperty("candidates").EnumerateArray()).GetProperty("tenantName").GetString());
        Assert.False(json.GetProperty("hostGuardInstalled").GetBoolean());

        foreach (var text in new[] { result, detail })
        {
            Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
            Assert.DoesNotContain("tenant-secret-canary", text, StringComparison.Ordinal);
            Assert.DoesNotContain("child-canary", text, StringComparison.Ordinal);
        }

        Assert.Equal(50, AgentToolTestContext.ReadJson(detail).GetProperty("policy").GetProperty("maxChildren").GetInt32());
    }

    [Fact]
    public async Task GetTenantHierarchyOverview_WithAnUnknownParent_SaysSo()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await InvokeAsync(new GetTenantHierarchyOverviewTool(), services, new() { ["parent"] = "missing" });

        // Assert
        Assert.Contains("No parent tenant is named 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveParentTenant_ChangesOnlyTheGivenFields_AndKeepsTheBlockedFeaturesAndRecipes()
    {
        // Arrange
        var parent = AddParent("firm", new ParentTenantPolicy
        {
            MaxChildren = 5,
            Recipes = ["Retail"],
            BlockedFeatures = ["CrestApps.OrchardCore.AI.Mcp.Local"],
            DeniedLocalPermissions = ["ManageTenants"],
            RequireMfa = true,
        });
        var child = AddChild("north", parent);
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new SaveParentTenantTool(), services, new()
        {
            ["tenantName"] = "firm",
            ["maxChildren"] = 200,
        }));

        // Assert
        var policy = parent.GetParentPolicy();

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.False(json.GetProperty("madeParent").GetBoolean());
        Assert.Equal(200, policy.MaxChildren);
        Assert.Equal(new[] { "Retail" }, policy.Recipes);
        Assert.Equal(new[] { "CrestApps.OrchardCore.AI.Mcp.Local" }, policy.BlockedFeatures);
        Assert.Equal(new[] { "ManageTenants" }, policy.DeniedLocalPermissions);
        Assert.True(policy.RequireMfa);
        Assert.Contains(parent, _updated);
        Assert.Contains(child, _updated);
    }

    [Fact]
    public async Task SaveParentTenant_RefusesARecipeTheHostDoesNotHave_AndASlugChange()
    {
        // Arrange
        AddParent("firm", new ParentTenantPolicy());
        var services = Build();

        // Act
        var recipe = await InvokeAsync(new SaveParentTenantTool(), services, new() { ["tenantName"] = "firm", ["recipes"] = new[] { "Unknown" } });
        var slug = await InvokeAsync(new SaveParentTenantTool(), services, new() { ["tenantName"] = "firm", ["slug"] = "other" });

        // Assert
        Assert.Contains("'Unknown' is not a setup recipe", recipe, StringComparison.Ordinal);
        Assert.Contains("set once", slug, StringComparison.Ordinal);
        Assert.Empty(_updated);
    }

    [Fact]
    public async Task SaveParentTenant_RefusesToMakeAParentWithoutTheHostGuard()
    {
        // Arrange
        _tenants.Add(TestShellSettings.Ordinary("shop"));
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new SaveParentTenantTool(), services, new()
        {
            ["tenantName"] = "shop",
            ["slug"] = "shop",
            ["displayName"] = "Shop",
        }));

        // Assert
        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains("AddTenantHierarchy()", json.GetProperty("errors")[0].GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(_updated);
    }

    [Fact]
    public async Task ChangeParentTenantState_SuspendsTheParentWithItsRunningChildren()
    {
        // Arrange
        var parent = AddParent("firm", new ParentTenantPolicy());
        var child = AddChild("north", parent);
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new ChangeParentTenantStateTool(), services, new() { ["tenantName"] = "firm", ["action"] = "Suspend" }));

        // Assert
        Assert.True(json.GetProperty("suspended").GetBoolean());
        Assert.Equal(TenantState.Disabled, parent.State);
        Assert.Equal(TenantState.Disabled, child.State);
    }

    [Fact]
    public async Task UnmakeParentTenant_RefusesAParentWithChildren()
    {
        // Arrange
        var parent = AddParent("firm", new ParentTenantPolicy());
        AddChild("north", parent);
        var services = Build();

        // Act
        var result = await InvokeAsync(new UnmakeParentTenantTool(), services, new() { ["tenantName"] = "firm" });

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.True(parent.IsParentTenant());
    }

    [Fact]
    public async Task UnmakeParentTenant_MakesAParentWithoutChildrenAnOrdinaryTenant()
    {
        // Arrange
        var parent = AddParent("firm", new ParentTenantPolicy());
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new UnmakeParentTenantTool(), services, new() { ["tenantName"] = "firm" }));

        // Assert
        Assert.True(json.GetProperty("unmade").GetBoolean());
        Assert.False(parent.IsParentTenant());
    }

    [Fact]
    public async Task RemoveParentTenant_NeedsTheExactTenantName()
    {
        // Arrange
        var parent = AddParent("firm", new ParentTenantPolicy());
        parent.State = TenantState.Disabled;
        var services = Build();

        // Act
        var result = await InvokeAsync(new RemoveParentTenantTool(), services, new() { ["tenantName"] = "firm", ["confirmName"] = "Firm" });

        // Assert
        Assert.StartsWith("Nothing was removed", result, StringComparison.Ordinal);
        _removalManager.Verify(manager => manager.RemoveAsync(It.IsAny<ShellSettings>(), It.IsAny<bool>()), Times.Never());
    }

    [Fact]
    public async Task RemoveParentTenant_RefusesARunningParent()
    {
        // Arrange
        AddParent("firm", new ParentTenantPolicy());
        var services = Build();

        // Act
        var result = await InvokeAsync(new RemoveParentTenantTool(), services, new() { ["tenantName"] = "firm", ["confirmName"] = "firm" });

        // Assert
        Assert.Contains("Suspend the parent tenant", result, StringComparison.Ordinal);
        _removalManager.Verify(manager => manager.RemoveAsync(It.IsAny<ShellSettings>(), It.IsAny<bool>()), Times.Never());
    }

    [Fact]
    public async Task MoveChildTenant_MovesAChildToTheNewParentsAddressPattern()
    {
        // Arrange
        var west = AddParent("west", new ParentTenantPolicy());
        AddParent("east", new ParentTenantPolicy());
        var child = AddChild("riverside", west);
        child[TenantHierarchyConstants.SettingsKeys.Slug] = "riverside";
        west.State = TenantState.Disabled;
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new MoveChildTenantTool(), services, new() { ["childTenantName"] = "riverside", ["newParentTenantName"] = "east" }));

        // Assert
        Assert.True(json.GetProperty("moved").GetBoolean());
        Assert.Equal("east", child[TenantHierarchyConstants.SettingsKeys.ParentTenant]);
        Assert.Equal("riverside.east.platform.com", child.RequestUrlHost);
    }

    [Fact]
    public async Task MoveChildTenant_WithAnUnknownParent_SaysSo()
    {
        // Arrange
        var west = AddParent("west", new ParentTenantPolicy());
        AddChild("riverside", west);
        var services = Build();

        // Act
        var result = await InvokeAsync(new MoveChildTenantTool(), services, new() { ["childTenantName"] = "riverside", ["newParentTenantName"] = "nowhere" });

        // Assert
        Assert.StartsWith("Nothing was moved", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetachChildTenant_DetachesAnOrphan_ButNotAChildWhoseParentExists()
    {
        // Arrange
        var parent = AddParent("firm", new ParentTenantPolicy());
        var child = AddChild("north", parent);
        var orphan = TestShellSettings.Child("lost", TestShellSettings.Parent("gone"));
        _tenants.Add(orphan);
        var services = Build();

        // Act
        var refused = await InvokeAsync(new DetachChildTenantTool(), services, new() { ["childTenantName"] = "north" });
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(new DetachChildTenantTool(), services, new() { ["childTenantName"] = "lost" }));

        // Assert
        Assert.Contains("is not an orphaned child tenant", refused, StringComparison.Ordinal);
        Assert.True(child.IsChildTenant());
        Assert.True(json.GetProperty("detached").GetBoolean());
        Assert.False(orphan.IsChildTenant());
    }

    private ShellSettings AddParent(string name, ParentTenantPolicy policy)
    {
        var settings = TestShellSettings.Parent(name);
        TenantHierarchySettingsWriter.WritePolicy(settings, policy);
        _tenants.Add(settings);

        return settings;
    }

    private ShellSettings AddChild(string name, ShellSettings parent)
    {
        var settings = TestShellSettings.Child(name, parent);
        _tenants.Add(settings);

        return settings;
    }

    private ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices();

        services.AddLogging();
        services.AddLocalization();
        services.AddOptions();
        services.Configure<TenantHierarchyOptions>(options => options.PlatformDomain = "platform.com");

        var defaultSettings = TestShellSettings.Default();
        services.AddSingleton(defaultSettings);

        var shellHost = new Mock<IShellHost>();
        shellHost.Setup(host => host.GetAllSettings()).Returns(() => [defaultSettings, .. _tenants]);
        shellHost.Setup(host => host.TryGetSettings(It.IsAny<string>(), out It.Ref<ShellSettings>.IsAny))
            .Returns(new TryGetSettingsCallback((string name, out ShellSettings settings) =>
            {
                settings = _tenants.FirstOrDefault(tenant => string.Equals(tenant.Name, name, StringComparison.OrdinalIgnoreCase));

                return settings is not null;
            }));
        shellHost.Setup(host => host.UpdateShellSettingsAsync(It.IsAny<ShellSettings>()))
            .Callback((ShellSettings settings) => _updated.Add(settings))
            .Returns(Task.CompletedTask);
        services.AddSingleton(shellHost.Object);
        services.AddSingleton(_removalManager.Object);

        var setupService = new Mock<ISetupService>();
        setupService.Setup(value => value.GetSetupRecipesAsync()).ReturnsAsync([new RecipeDescriptor { Name = "Retail" }]);
        services.AddSingleton(setupService.Object);

        services.AddSingleton<ChildDatabaseProvisioning>();
        services.AddSingleton<IChildTenantDatabaseProvisioner, SqliteOnlyProvisioner>();
        services.AddSingleton(Mock.Of<IWebHostEnvironment>());
        services.AddSingleton(Mock.Of<IClock>());
        services.AddSingleton<TenantHierarchyPlatformService>();

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(AIFunction tool, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(tool, services, arguments);

    private sealed class SqliteOnlyProvisioner : IChildTenantDatabaseProvisioner
    {
        public bool CanProvision(ChildDatabaseProvisioningContext context)
            => context.Strategy == ChildDatabaseStrategy.SqlitePerChild;

        public Task<ChildDatabaseProvisioningResult> ProvisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeprovisionAsync(ChildDatabaseProvisioningContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
