using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Admin;
using CrestApps.OrchardCore.AI.Agent.AdminMenus;
using CrestApps.OrchardCore.AI.Agent.Deployment;
using CrestApps.OrchardCore.AI.Agent.Settings;
using CrestApps.OrchardCore.AI.Agent.TenantFeatureProfiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Agent.SiteAdministration;

/// <summary>
/// Every site administration tool acts for the person in the chat: it refuses a caller who is not signed in without
/// checking anything, and a caller who lacks the permission the matching admin page asks for, with the resource that
/// page passes, and it changes nothing when it refuses.
/// </summary>
public sealed class SiteAdministrationToolCallerTests
{
    public static TheoryData<string, string, string> Tools => new()
    {
        { ListSiteSettingsGroupsTool.TheName, "AccessAdminPanel", null },
        { GetSiteSettingsTool.TheName, "ManageResourceSettings", SiteAdministrationToolHarness.GroupId },
        { UpdateSiteSettingsTool.TheName, "ManageResourceSettings", SiteAdministrationToolHarness.GroupId },
        { FindAdminPageTool.TheName, "AccessAdminPanel", null },
        { ListAdminMenusTool.TheName, "ManageAdminMenu", null },
        { SaveAdminMenuTool.TheName, "ManageAdminMenu", null },
        { ListTenantFeatureProfilesTool.TheName, "ManageTenantFeatureProfiles", null },
        { SaveTenantFeatureProfileTool.TheName, "ManageTenantFeatureProfiles", null },
        { ListDeploymentPlansTool.TheName, "ManageDeploymentPlan", null },
        { SaveDeploymentPlanTool.TheName, "ManageDeploymentPlan", null },
        { DeleteDeploymentPlanTool.TheName, "ManageDeploymentPlan", null },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAnyCheck(string toolName, string permission, string resource)
    {
        // Arrange
        _ = permission;
        _ = resource;

        var harness = new SiteAdministrationToolHarness
        {
            SignedIn = false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
        AssertNothingChanged(harness);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminPage_Refuses(string toolName, string permission, string resource)
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, check.Permission);
        Assert.Equal(resource, check.Resource);
        AssertNothingChanged(harness);
    }

    private static void AssertNothingChanged(SiteAdministrationToolHarness harness)
    {
        Assert.Equal(0, harness.SiteSaves);
        Assert.Empty(harness.RecipeSteps);
        Assert.Empty(harness.DeletedPlans);
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
        => toolName switch
        {
            ListSiteSettingsGroupsTool.TheName => (new ListSiteSettingsGroupsTool(), []),
            GetSiteSettingsTool.TheName => (new GetSiteSettingsTool(), new() { ["groupId"] = SiteAdministrationToolHarness.GroupId }),
            UpdateSiteSettingsTool.TheName => (new UpdateSiteSettingsTool(), new()
            {
                ["groupId"] = SiteAdministrationToolHarness.GroupId,
                ["changes"] = JsonNode.Parse("""{ "ISite.TestSettings": { "ShowBanner": true } }"""),
            }),
            FindAdminPageTool.TheName => (new FindAdminPageTool(), new() { ["search"] = "settings" }),
            ListAdminMenusTool.TheName => (new ListAdminMenusTool(), []),
            SaveAdminMenuTool.TheName => (new SaveAdminMenuTool(), new() { ["name"] = "Reports", ["menuItems"] = new JsonArray() }),
            ListTenantFeatureProfilesTool.TheName => (new ListTenantFeatureProfilesTool(), []),
            SaveTenantFeatureProfileTool.TheName => (new SaveTenantFeatureProfileTool(), new() { ["name"] = "Small", ["rules"] = new JsonArray() }),
            ListDeploymentPlansTool.TheName => (new ListDeploymentPlansTool(), []),
            SaveDeploymentPlanTool.TheName => (new SaveDeploymentPlanTool(), new() { ["name"] = "Plan", ["steps"] = new JsonArray() }),
            DeleteDeploymentPlanTool.TheName => (new DeleteDeploymentPlanTool(), new() { ["id"] = 7 }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
