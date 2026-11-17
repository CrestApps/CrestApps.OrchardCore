using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.AdminMenus;
using CrestApps.OrchardCore.AI.Agent.TenantFeatureProfiles;

namespace CrestApps.OrchardCore.Tests.Agent.SiteAdministration;

/// <summary>
/// The admin menu tools read the menus through the admin menu deployment step and save one through the
/// <c>AdminMenu</c> recipe step; the feature profile tools read the profiles with the tenants that use them and save
/// one through the <c>FeatureProfiles</c> recipe step, refusing a name another profile has.
/// </summary>
public sealed class AdminMenuAndProfileToolsTests
{
    [Fact]
    public async Task ListAdminMenus_WithNodes_ReturnsTheTreeInTheShapeTheSaveTakes()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAdminMenusTool(), services, new() { ["includeNodes"] = true });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var menu = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("menu-1", menu.GetProperty("id").GetString());
        Assert.Equal("Shortcuts", menu.GetProperty("name").GetString());
        Assert.Equal(1, menu.GetProperty("nodeCount").GetInt32());
        Assert.Equal("LinkAdminNode", menu.GetProperty("menuItems")[0].GetProperty("$type").GetString());
    }

    [Fact]
    public async Task ListAdminMenus_WhenTheMenuIdIsUnknown_SaysSo()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAdminMenusTool(), services, new() { ["menuId"] = "nope" });

        // Assert
        Assert.StartsWith("There is no admin menu with the ID 'nope'", result);
    }

    [Fact]
    public async Task SaveAdminMenu_RunsTheAdminMenuStepWithANewId()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAdminMenuTool(), services, new()
        {
            ["name"] = "Reports",
            ["menuItems"] = JsonNode.Parse("""[ { "$type": "LinkAdminNode", "LinkText": "Sales", "LinkUrl": "/Admin/Reports/Sales" } ]"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var step = Assert.Single(harness.RecipeSteps);
        var menu = step.Step["data"][0];

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("AdminMenu", step.Name);
        Assert.Equal(json.GetProperty("id").GetString(), menu["Id"].GetValue<string>());
        Assert.Equal("Reports", menu["Name"].GetValue<string>());
        Assert.Equal(1, json.GetProperty("nodeCount").GetInt32());
    }

    [Fact]
    public async Task SaveAdminMenu_WhenTheIdIsUnknown_SavesNothing()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAdminMenuTool(), services, new()
        {
            ["id"] = "nope",
            ["name"] = "Reports",
            ["menuItems"] = new JsonArray(),
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Empty(harness.RecipeSteps);
    }

    [Fact]
    public async Task ListTenantFeatureProfiles_ReturnsTheRulesAndTheTenantsThatUseEachProfile()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListTenantFeatureProfilesTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var profile = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("profile-1", profile.GetProperty("id").GetString());
        Assert.Equal("Exclude", profile.GetProperty("rules")[0].GetProperty("rule").GetString());
        Assert.Equal("acme", Assert.Single(profile.GetProperty("tenants").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task ListTenantFeatureProfiles_OutsideTheDefaultTenant_Refuses()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness
        {
            IsDefaultTenant = false,
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListTenantFeatureProfilesTool(), services);

        // Assert
        Assert.Equal("Tenant feature profiles are managed from the Default tenant only.", result);
    }

    [Fact]
    public async Task SaveTenantFeatureProfile_RunsTheFeatureProfilesStep()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTenantFeatureProfileTool(), services, new()
        {
            ["name"] = "Small sites",
            ["rules"] = JsonNode.Parse("""[ { "rule": "include", "expression": "OrchardCore.Contents" } ]"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var step = Assert.Single(harness.RecipeSteps);
        var id = json.GetProperty("id").GetString();
        var profile = step.Step["FeatureProfiles"][id];

        Assert.Equal("FeatureProfiles", step.Name);
        Assert.Equal("Small sites", profile["Name"].GetValue<string>());
        Assert.Equal("Include", profile["FeatureRules"][0]["Rule"].GetValue<string>());
    }

    [Fact]
    public async Task SaveTenantFeatureProfile_WhenAnotherProfileHasTheName_SavesNothing()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTenantFeatureProfileTool(), services, new()
        {
            ["name"] = "No search",
            ["rules"] = new JsonArray(),
        });

        // Assert
        Assert.Equal("Nothing was saved: a feature profile named 'No search' already exists.", result);
        Assert.Empty(harness.RecipeSteps);
    }
}
