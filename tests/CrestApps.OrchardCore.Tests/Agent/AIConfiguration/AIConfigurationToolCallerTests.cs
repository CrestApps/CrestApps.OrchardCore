using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Personas;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Agent.ProfileTemplates;
using CrestApps.OrchardCore.AI.Agent.ToolInstances;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Agent.AIConfiguration;

/// <summary>
/// Every AI profile, template, persona and tool instance tool acts for the person in the chat: it refuses a caller who
/// is not signed in without checking anything, and a caller who lacks the permission the matching admin page asks for,
/// and it changes nothing when it refuses.
/// </summary>
public sealed class AIConfigurationToolCallerTests
{
    public static TheoryData<string, string> Tools => new()
    {
        { ListAIToolsTool.TheName, "ManageAIProfiles" },
        { SaveAIProfileTool.TheName, "ManageAIProfiles" },
        { SetAIProfileCapabilitiesTool.TheName, "ManageAIProfiles" },
        { CreateAIProfileFromTemplateTool.TheName, "ManageAIProfiles" },
        { DeleteAIProfileTool.TheName, "ManageAIProfiles" },
        { ListAIProfileVersionsTool.TheName, "ManageAIProfiles" },
        { RestoreAIProfileVersionTool.TheName, "ManageAIProfiles" },
        { TestAIProfileTool.TheName, "QueryAnyAIProfile" },
        { ListAIProfileTemplatesTool.TheName, "ManageAIProfileTemplates" },
        { GetAIProfileTemplateTool.TheName, "ManageAIProfileTemplates" },
        { SaveAIProfileTemplateTool.TheName, "ManageAIProfileTemplates" },
        { DeleteAIProfileTemplateTool.TheName, "ManageAIProfileTemplates" },
        { ListAIPersonasTool.TheName, "ManageAIPersonas" },
        { SaveAIPersonaTool.TheName, "ManageAIPersonas" },
        { DeleteAIPersonaTool.TheName, "ManageAIPersonas" },
        { ListAIToolInstancesTool.TheName, "ManageAIToolInstances" },
        { SaveAIToolInstanceTool.TheName, "ManageAIToolInstances" },
        { DeleteAIToolInstanceTool.TheName, "ManageAIToolInstances" },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAnyCheck(string toolName, string permission)
    {
        // Arrange
        _ = permission;

        var harness = CreateHarness();
        harness.SignedIn = false;

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(harness.Checks);
        AssertNothingChanged(harness);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminPage_Refuses(string toolName, string permission)
    {
        // Arrange
        var harness = CreateHarness();
        harness.Authorize = (_, _) => false;

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, harness.Checks[0].Permission);
        AssertNothingChanged(harness);
    }

    [Fact]
    public async Task TestAIProfile_ChecksQueryPermissionOnTheProfile()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Authorize = (_, _) => false;

        using var services = harness.Build();
        var (tool, arguments) = Create(TestAIProfileTool.TheName);

        // Act
        await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var check = Assert.Single(harness.Checks);

        Assert.Equal("QueryAnyAIProfile", check.Permission);
        Assert.Equal("support", Assert.IsType<AIProfile>(check.Resource).ItemId);
        Assert.Empty(harness.Completions);
    }

    private static AIConfigurationToolHarness CreateHarness()
    {
        var harness = new AIConfigurationToolHarness();

        harness.AddProfile("support", "support");
        harness.Templates.Add(new AIProfileTemplate { ItemId = "template-1", Name = "template-1", Source = "SystemPrompt", DisplayText = "Template" });
        harness.Personas.Add(new AIPersona { ItemId = "persona-1", Name = "maya", Instructions = "Be warm." });
        harness.Instances.Add(new AIToolInstance { ItemId = "instance-1", Name = "orders", Source = "sitemap-documentation", Description = "Orders" });
        harness.Versions.Add(new AIProfileVersion { ItemId = "version-1", ProfileId = "support", Version = 1, Snapshot = new AIProfile { ItemId = "support", Name = "support" } });

        return harness;
    }

    private static void AssertNothingChanged(AIConfigurationToolHarness harness)
    {
        Assert.Single(harness.Profiles.All);
        Assert.Empty(harness.Profiles.Updated);
        Assert.Empty(harness.Profiles.Deleted);
        Assert.Single(harness.Templates);
        Assert.Single(harness.Personas);
        Assert.Single(harness.Instances);
        Assert.Empty(harness.InstanceUpdates);
        Assert.Empty(harness.Completions);
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
        => toolName switch
        {
            ListAIToolsTool.TheName => (new ListAIToolsTool(), []),
            SaveAIProfileTool.TheName => (new SaveAIProfileTool(), new() { ["profileId"] = "support", ["title"] = "Changed" }),
            SetAIProfileCapabilitiesTool.TheName => (new SetAIProfileCapabilitiesTool(), new() { ["profileId"] = "support", ["toolNames"] = new[] { AIConfigurationToolHarness.SafeToolName } }),
            CreateAIProfileFromTemplateTool.TheName => (new CreateAIProfileFromTemplateTool(), new() { ["templateId"] = "template-1", ["title"] = "New", ["name"] = "new" }),
            DeleteAIProfileTool.TheName => (new DeleteAIProfileTool(), new() { ["profileIds"] = new[] { "support" } }),
            ListAIProfileVersionsTool.TheName => (new ListAIProfileVersionsTool(), new() { ["profileId"] = "support" }),
            RestoreAIProfileVersionTool.TheName => (new RestoreAIProfileVersionTool(), new() { ["profileId"] = "support", ["versionId"] = "version-1" }),
            TestAIProfileTool.TheName => (new TestAIProfileTool(), new() { ["profileId"] = "support", ["message"] = "Hi" }),
            ListAIProfileTemplatesTool.TheName => (new ListAIProfileTemplatesTool(), []),
            GetAIProfileTemplateTool.TheName => (new GetAIProfileTemplateTool(), new() { ["templateId"] = "template-1" }),
            SaveAIProfileTemplateTool.TheName => (new SaveAIProfileTemplateTool(), new() { ["templateId"] = "template-1", ["title"] = "Changed" }),
            DeleteAIProfileTemplateTool.TheName => (new DeleteAIProfileTemplateTool(), new() { ["templateIds"] = new[] { "template-1" } }),
            ListAIPersonasTool.TheName => (new ListAIPersonasTool(), []),
            SaveAIPersonaTool.TheName => (new SaveAIPersonaTool(), new() { ["personaId"] = "persona-1", ["title"] = "Changed" }),
            DeleteAIPersonaTool.TheName => (new DeleteAIPersonaTool(), new() { ["personaIds"] = new[] { "persona-1" } }),
            ListAIToolInstancesTool.TheName => (new ListAIToolInstancesTool(), []),
            SaveAIToolInstanceTool.TheName => (new SaveAIToolInstanceTool(), new() { ["instanceId"] = "instance-1", ["description"] = "Changed" }),
            DeleteAIToolInstanceTool.TheName => (new DeleteAIToolInstanceTool(), new() { ["instanceIds"] = new[] { "instance-1" } }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
