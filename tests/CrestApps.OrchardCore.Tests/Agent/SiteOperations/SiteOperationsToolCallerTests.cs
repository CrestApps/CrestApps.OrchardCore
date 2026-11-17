using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.BackgroundTasks;
using CrestApps.OrchardCore.AI.Agent.DataLocalization;
using CrestApps.OrchardCore.AI.Agent.HealthChecks;
using CrestApps.OrchardCore.AI.Agent.UrlRewriting;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Agent.SiteOperations;

/// <summary>
/// Every site operations tool acts for the person in the chat: it refuses a caller who is not signed in without
/// checking anything, and a caller who lacks the permission the matching admin page asks for, and it changes nothing
/// when it refuses.
/// </summary>
public sealed class SiteOperationsToolCallerTests
{
    public static TheoryData<string, string> Tools => new()
    {
        { ListBackgroundTasksTool.TheName, "ManageBackgroundTasks" },
        { RunBackgroundTaskTool.TheName, "ManageBackgroundTasks" },
        { ListUrlRewriteRulesTool.TheName, "ManageUrlRewritingRules" },
        { SaveUrlRewriteRuleTool.TheName, "ManageUrlRewritingRules" },
        { DeleteUrlRewriteRuleTool.TheName, "ManageUrlRewritingRules" },
        { GetSiteHealthTool.TheName, "AccessAdminPanel" },
        { ListDynamicTranslationsTool.TheName, "ViewDynamicTranslations" },
        { SaveDynamicTranslationsTool.TheName, "ManageTranslations" },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAnyCheck(string toolName, string permission)
    {
        // Arrange
        _ = permission;

        var harness = new SiteOperationsToolHarness
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
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminPage_Refuses(string toolName, string permission)
    {
        // Arrange
        var harness = new SiteOperationsToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, checks[0].Permission);
        Assert.Null(checks[0].Resource);
        AssertNothingChanged(harness);
    }

    [Fact]
    public async Task SaveDynamicTranslations_WhenTheCallerHasNeitherPermission_ChecksBothAndRefuses()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(SaveDynamicTranslationsTool.TheName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(["ManageTranslations", "ManageTranslations_fr-FR"], checks.Select(check => check.Permission));
        Assert.Empty(harness.RecipeSteps);
    }

    [Fact]
    public async Task SaveDynamicTranslations_WhenTheCallerMayEditOnlyThatCulture_Saves()
    {
        // Arrange
        var harness = new SiteOperationsToolHarness
        {
            Authorize = (permission, _) => permission.Name == "ManageTranslations_fr-FR",
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(SaveDynamicTranslationsTool.TheName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.NotEqual(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Single(harness.RecipeSteps);
    }

    private static void AssertNothingChanged(SiteOperationsToolHarness harness)
    {
        Assert.Equal(0, harness.Releases);
        Assert.Empty(harness.RunRequests);
        Assert.Empty(harness.RecipeSteps);
        Assert.Equal([SiteOperationsToolHarness.RedirectRuleId], harness.Rules.Keys);
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
        => toolName switch
        {
            ListBackgroundTasksTool.TheName => (new ListBackgroundTasksTool(), []),
            RunBackgroundTaskTool.TheName => (new RunBackgroundTaskTool(), new() { ["names"] = new[] { typeof(SiteOperationsToolHarness.IndexingTestBackgroundTask).FullName } }),
            ListUrlRewriteRulesTool.TheName => (new ListUrlRewriteRulesTool(), []),
            SaveUrlRewriteRuleTool.TheName => (new SaveUrlRewriteRuleTool(), new()
            {
                ["type"] = "Redirect",
                ["name"] = "Sale",
                ["pattern"] = "^sale$",
                ["substitutionPattern"] = "/offers",
            }),
            DeleteUrlRewriteRuleTool.TheName => (new DeleteUrlRewriteRuleTool(), new() { ["ids"] = new[] { SiteOperationsToolHarness.RedirectRuleId } }),
            GetSiteHealthTool.TheName => (new GetSiteHealthTool(), []),
            ListDynamicTranslationsTool.TheName => (new ListDynamicTranslationsTool(), new() { ["culture"] = "fr-FR" }),
            SaveDynamicTranslationsTool.TheName => (new SaveDynamicTranslationsTool(), new()
            {
                ["culture"] = "fr-FR",
                ["translations"] = JsonNode.Parse("""[ { "context": "Content Types", "key": "Article", "value": "Article" } ]"""),
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
