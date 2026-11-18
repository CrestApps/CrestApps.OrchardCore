using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Agent.AIModels;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Agent.AIModels;

/// <summary>
/// No result of the provider connection, deployment and voice tools carries a key. Every secret field of the fixtures
/// (each connection metadata shape, a configuration connection's flat key, a deployment's contained key and other
/// properties) holds a canary value, and none may appear in any result, errors included. A key is never accepted
/// either, and the stored one survives an update.
/// </summary>
public sealed class AIModelSecretCanaryTests
{
    private static readonly string[] _canaries =
    [
        "CANARY-OPENAI-KEY",
        "CANARY-AZURE-KEY",
        "CANARY-LEGACY-AZURE-KEY",
        "CANARY-INFERENCE-KEY",
        "CANARY-CONFIG-CONNECTION-KEY",
        "CANARY-CONNECTION-HEADER",
        "CANARY-ELEVENLABS-KEY",
        "CANARY-SPEECH-KEY",
        "CANARY-CONFIG-DEPLOYMENT-KEY",
        "CANARY-DEPLOYMENT-PROPERTY",
        "CANARY-ARGUMENT-KEY",
    ];

    public static TheoryData<string, Dictionary<string, object>> Calls => new()
    {
        { ListAIProviderConnectionsTool.TheName, new() { ["includeProviders"] = true } },
        { SaveAIProviderConnectionTool.TheName, new() { ["connectionId"] = "openai-1", ["title"] = "OpenAI" } },
        { SaveAIProviderConnectionTool.TheName, new() { ["connectionId"] = "azure-1", ["endpoint"] = "https://contoso.openai.azure.com/" } },
        { SaveAIProviderConnectionTool.TheName, new() { ["connectionId"] = "openai-1", ["apiKey"] = "CANARY-ARGUMENT-KEY" } },
        { SaveAIProviderConnectionTool.TheName, new() { ["provider"] = "OpenAI", ["name"] = "openai-2", ["apiKey"] = "CANARY-ARGUMENT-KEY" } },
        { SaveAIProviderConnectionTool.TheName, new() { ["connectionId"] = "config-1", ["title"] = "Config" } },
        { DeleteAIProviderConnectionTool.TheName, new() { ["connectionIds"] = new[] { "legacy-1", "config-1" } } },
        { ListAIDeploymentsTool.TheName, new() { ["includeOptions"] = true } },
        { SaveAIDeploymentTool.TheName, new() { ["deploymentId"] = "speech-1", ["endpoint"] = "https://api.elevenlabs.io/" } },
        { SaveAIDeploymentTool.TheName, new() { ["deploymentId"] = "speech-1", ["apiKey"] = "CANARY-ARGUMENT-KEY" } },
        { SaveAIDeploymentTool.TheName, new() { ["deploymentId"] = "config-deployment", ["model"] = "x" } },
        { SetAIDeploymentPricingTool.TheName, new() { ["deploymentId"] = "speech-1", ["prices"] = JsonNode.Parse("""[{ "meter": "Characters", "amount": 30 }]""") } },
        { DeleteAIDeploymentTool.TheName, new() { ["deploymentIds"] = new[] { "speech-1", "config-deployment" } } },
        { ListAIVoicesTool.TheName, new() { ["deploymentName"] = "voices" } },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Result_NeverCarriesAKey(string toolName, Dictionary<string, object> arguments)
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(Create(toolName), services, arguments);

        // Assert
        Assert.False(string.IsNullOrEmpty(result));

        foreach (var canary in _canaries)
        {
            Assert.DoesNotContain(canary, result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ListAIProviderConnections_ReportsAStoredKeyInEveryShape()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIProviderConnectionsTool(), services);

        // Assert
        var items = AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(5, items.Count);
        Assert.All(items, item => Assert.True(item.GetProperty("hasApiKey").GetBoolean(), item.GetProperty("id").GetString()));
        Assert.True(items.Single(item => item.GetProperty("id").GetString() == "config-1").GetProperty("fromConfiguration").GetBoolean());
    }

    [Fact]
    public async Task SaveAIDeployment_KeepsTheContainedKeyOnAnUpdate()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDeploymentTool(), services, new() { ["deploymentId"] = "speech-1", ["model"] = "eleven_turbo_v2" });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("CANARY-ELEVENLABS-KEY", harness.Deployments.Single(deployment => deployment.ItemId == "speech-1").Properties["ApiKey"]);
    }

    private static AIModelToolHarness CreateHarness()
    {
        var harness = new AIModelToolHarness();

        harness.AddConnection("openai-1", AIModelToolHarness.OpenAI, "openai", protectedKey: "CANARY-OPENAI-KEY", configure: connection =>
            connection.Properties["CustomHeaders"] = new JsonObject { ["X-Secret"] = "CANARY-CONNECTION-HEADER" });
        harness.AddConnection("azure-1", AIModelToolHarness.Azure, "azure", protectedKey: "CANARY-AZURE-KEY");
        harness.AddConnection("legacy-1", AIModelToolHarness.Azure, "legacy", protectedKey: null, configure: connection =>
        {
            connection.Properties.Clear();
            connection.Properties["AzureOpenAIConnectionMetadata"] = new JsonObject { ["Endpoint"] = "https://legacy.example.com/", ["ApiKey"] = "CANARY-LEGACY-AZURE-KEY" };
        });
        harness.AddConnection("inference-1", AIModelToolHarness.Azure, "inference", protectedKey: null, configure: connection =>
        {
            connection.Properties.Clear();
            connection.Properties["AzureAIInferenceConnectionMetadata"] = new JsonObject { ["Endpoint"] = "https://inference.example.com/", ["ApiKey"] = "CANARY-INFERENCE-KEY", ["AuthenticationType"] = 1 };
        });
        harness.AddConnection("config-1", AIModelToolHarness.OpenAI, "config", protectedKey: null, configure: connection =>
        {
            connection.IsReadOnly = true;
            connection.Properties.Clear();
            connection.Properties["Endpoint"] = JsonSerializer.SerializeToElement("https://config.example.com/");
            connection.Properties["ApiKey"] = JsonSerializer.SerializeToElement("CANARY-CONFIG-CONNECTION-KEY");
        });

        harness.AddDeployment("speech-1", AIModelToolHarness.ElevenLabs, "voices", configure: deployment =>
        {
            deployment.Put(new AIDeploymentMetadata { Features = ["textToSpeech"] });
            deployment.Properties["Endpoint"] = "https://api.elevenlabs.io/";
            deployment.Properties["ApiKey"] = "CANARY-ELEVENLABS-KEY";
            deployment.Properties["Notes"] = "CANARY-DEPLOYMENT-PROPERTY";
        });
        harness.AddDeployment("azure-speech", AIModelToolHarness.ElevenLabs, "azure-speech", configure: deployment =>
            deployment.Properties["ApiKey"] = "CANARY-SPEECH-KEY");
        harness.AddDeployment("config-deployment", AIModelToolHarness.OpenAI, "config-gpt", "config", deployment =>
        {
            deployment.IsReadOnly = true;
            deployment.Properties["ApiKey"] = JsonSerializer.SerializeToElement("CANARY-CONFIG-DEPLOYMENT-KEY");
        });

        return harness;
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ListAIProviderConnectionsTool.TheName => new ListAIProviderConnectionsTool(),
            SaveAIProviderConnectionTool.TheName => new SaveAIProviderConnectionTool(),
            DeleteAIProviderConnectionTool.TheName => new DeleteAIProviderConnectionTool(),
            ListAIDeploymentsTool.TheName => new ListAIDeploymentsTool(),
            SaveAIDeploymentTool.TheName => new SaveAIDeploymentTool(),
            SetAIDeploymentPricingTool.TheName => new SetAIDeploymentPricingTool(),
            DeleteAIDeploymentTool.TheName => new DeleteAIDeploymentTool(),
            ListAIVoicesTool.TheName => new ListAIVoicesTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
