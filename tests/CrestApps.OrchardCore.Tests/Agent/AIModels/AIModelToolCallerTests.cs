using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Agent.AIModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Agent.AIModels;

/// <summary>
/// Every provider connection, deployment and voice tool acts for the person in the chat: it refuses a caller who is
/// not signed in and a caller who lacks the permission the matching admin page asks for, and changes nothing then.
/// </summary>
public sealed class AIModelToolCallerTests
{
    public static TheoryData<string, string> Tools => new()
    {
        { ListAIProviderConnectionsTool.TheName, "ManageProviderConnections" },
        { SaveAIProviderConnectionTool.TheName, "ManageProviderConnections" },
        { DeleteAIProviderConnectionTool.TheName, "ManageProviderConnections" },
        { ListAIDeploymentsTool.TheName, "ManageAIDeployments" },
        { SaveAIDeploymentTool.TheName, "ManageAIDeployments" },
        { SetAIDeploymentPricingTool.TheName, "ManageAIDeployments" },
        { DeleteAIDeploymentTool.TheName, "ManageAIDeployments" },
        { ListAIVoicesTool.TheName, "ManageAIDeployments" },
        { CloneAIVoiceTool.TheName, "ManageAIDeployments" },
        { DeleteAIVoiceTool.TheName, "ManageAIDeployments" },
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
        Assert.Equal(permission, harness.Checks[0]);
        AssertNothingChanged(harness);
    }

    private static AIModelToolHarness CreateHarness()
    {
        var harness = new AIModelToolHarness();

        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");
        harness.AddDeployment("speech-1", AIModelToolHarness.ElevenLabs, "voices", configure: deployment => deployment.Put(new AIDeploymentMetadata { Features = ["textToSpeech"] }));
        harness.Media.AddFile("voices/ceo.mp3", [1, 2, 3]);

        return harness;
    }

    private static void AssertNothingChanged(AIModelToolHarness harness)
    {
        Assert.Single(harness.Connections);
        Assert.Equal(2, harness.Deployments.Count);
        Assert.Empty(harness.ConnectionPayloads);
        Assert.Empty(harness.UpdatedDeployments);
        Assert.Equal(0, harness.ReleaseRequests);
        Assert.Empty(harness.Voices.Searches);
        Assert.Empty(harness.Voices.Clones);
        Assert.Empty(harness.Voices.Deleted);
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
        => toolName switch
        {
            ListAIProviderConnectionsTool.TheName => (new ListAIProviderConnectionsTool(), []),
            SaveAIProviderConnectionTool.TheName => (new SaveAIProviderConnectionTool(), new() { ["connectionId"] = "connection-1", ["title"] = "Changed" }),
            DeleteAIProviderConnectionTool.TheName => (new DeleteAIProviderConnectionTool(), new() { ["connectionIds"] = new[] { "connection-1" } }),
            ListAIDeploymentsTool.TheName => (new ListAIDeploymentsTool(), []),
            SaveAIDeploymentTool.TheName => (new SaveAIDeploymentTool(), new() { ["deploymentId"] = "deployment-1", ["model"] = "gpt-4.1" }),
            SetAIDeploymentPricingTool.TheName => (new SetAIDeploymentPricingTool(), new() { ["deploymentId"] = "deployment-1", ["prices"] = Array.Empty<object>() }),
            DeleteAIDeploymentTool.TheName => (new DeleteAIDeploymentTool(), new() { ["deploymentIds"] = new[] { "deployment-1" } }),
            ListAIVoicesTool.TheName => (new ListAIVoicesTool(), new() { ["deploymentName"] = "voices" }),
            CloneAIVoiceTool.TheName => (new CloneAIVoiceTool(), new() { ["deploymentName"] = "voices", ["name"] = "CEO", ["mediaPath"] = "voices/ceo.mp3" }),
            DeleteAIVoiceTool.TheName => (new DeleteAIVoiceTool(), new() { ["deploymentName"] = "voices", ["voiceId"] = "voice-calm" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
