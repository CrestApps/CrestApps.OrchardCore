using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Agent.AIModels;
using CrestApps.OrchardCore.Tests.Agent.AIConfiguration;

namespace CrestApps.OrchardCore.Tests.Agent.AIModels;

/// <summary>
/// The provider connection, deployment and voice tools do what their admin pages do, refuse what the pages refuse,
/// never take or show a key, and leave alone the models the running assistant talks to.
/// </summary>
public sealed class AIModelToolsTests
{
    [Fact]
    public async Task ListAIProviderConnections_ReportsTheKeyAsStoredAndTheDeploymentsThatUseIt()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIProviderConnectionsTool(), services, new() { ["includeProviders"] = true });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.True(item.GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("https://api.example.com/", item.GetProperty("endpoint").GetString());
        Assert.Equal("gpt-4o", Assert.Single(item.GetProperty("deployments").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(2, json.GetProperty("providers").GetArrayLength());
        Assert.DoesNotContain("CipherText", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAIProviderConnectionsAndDeployments_AFilterThatMatchesNothingIsEmpty()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");

        using var services = harness.Build();

        // Act
        var connections = await AgentToolTestContext.InvokeAsync(new ListAIProviderConnectionsTool(), services, new() { ["provider"] = AIModelToolHarness.Azure });
        var deployments = await AgentToolTestContext.InvokeAsync(new ListAIDeploymentsTool(), services, new() { ["capability"] = "realtime" });

        // Assert
        Assert.Equal(0, AgentToolTestContext.ReadJson(connections).GetProperty("total").GetInt32());
        Assert.Empty(AgentToolTestContext.ReadJson(deployments).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task SaveAIDeployment_CreatesADeploymentOnAConnectionWithItsCapabilities()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDeploymentTool(), services, new()
        {
            ["provider"] = AIModelToolHarness.OpenAI,
            ["name"] = "gpt-4o-mini",
            ["model"] = "gpt-4o-mini",
            ["capabilities"] = new[] { "textGeneration", "ToolCalling" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var deployment = Assert.Single(harness.Deployments);

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("openai", deployment.ConnectionName);
        Assert.Equal(["textGeneration", "toolCalling"], deployment.GetOrCreate<AIDeploymentMetadata>().Features);
    }

    [Fact]
    public async Task SaveAIDeployment_RefusesAnUnknownCapability()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDeploymentTool(), services, new()
        {
            ["provider"] = AIModelToolHarness.OpenAI,
            ["name"] = "gpt-4o-mini",
            ["model"] = "gpt-4o-mini",
            ["capabilities"] = new[] { "telepathy" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains("telepathy", result, StringComparison.Ordinal);
        Assert.Empty(harness.Deployments);
    }

    [Fact]
    public async Task SaveAIDeployment_ASpeechDeploymentIsSavedWithoutAKeyAndSaysSo()
    {
        // Arrange
        var harness = new AIModelToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDeploymentTool(), services, new()
        {
            ["provider"] = AIModelToolHarness.ElevenLabs,
            ["name"] = "voices",
            ["model"] = "eleven_multilingual_v2",
            ["capabilities"] = new[] { "textToSpeech" },
            ["endpoint"] = "https://api.elevenlabs.io/",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var deployment = Assert.Single(harness.Deployments);

        Assert.False(json.GetProperty("hasApiKey").GetBoolean());
        Assert.Contains("No key is stored", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Null(deployment.ConnectionName);
        Assert.Equal("https://api.elevenlabs.io/", deployment.Properties["Endpoint"]);
        Assert.False(deployment.Properties.ContainsKey("ApiKey"));
    }

    [Fact]
    public async Task SaveAIDeployment_InsideATurn_RefusesTheRunningAssistantsDeployment()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");
        harness.Defaults.DefaultChatDeploymentName = "gpt-4o";
        var assistant = new AIProfile { ItemId = "assistant", Name = "assistant" };
        harness.Profiles.Add(assistant);

        using var services = harness.Build();
        using var turn = AIConfigurationToolHarness.BeginTurn(assistant);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIDeploymentTool(), services, new() { ["deploymentId"] = "deployment-1", ["model"] = "gpt-3.5" });

        // Assert
        Assert.StartsWith("Refused", result, StringComparison.Ordinal);
        Assert.Equal("gpt-4o", harness.Deployments[0].ModelName);
        Assert.Empty(harness.UpdatedDeployments);
    }

    [Fact]
    public async Task SetAIDeploymentPricing_SetsThePricesEvenOnTheRunningAssistantsDeployment()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");
        harness.Defaults.DefaultChatDeploymentName = "gpt-4o";
        var assistant = new AIProfile { ItemId = "assistant", Name = "assistant" };
        harness.Profiles.Add(assistant);

        using var services = harness.Build();
        using var turn = AIConfigurationToolHarness.BeginTurn(assistant);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetAIDeploymentPricingTool(), services, new()
        {
            ["deploymentId"] = "deployment-1",
            ["prices"] = JsonNode.Parse("""[{ "meter": "InputTokens", "amount": 2.5 }, { "meter": "OutputTokens", "amount": 10, "per": 1000000 }]"""),
        });

        // Assert
        var pricing = harness.Deployments[0].GetPricing();

        Assert.NotNull(pricing);
        Assert.Equal("USD", pricing.Currency);
        Assert.Equal(2, pricing.Prices.Count);
        Assert.Equal(1_000_000, pricing.Prices[0].Per);
        Assert.Single(harness.UpdatedDeployments);
        Assert.Contains("cost estimates", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""[{ "meter": "AdditionalCount", "amount": 1 }]""", "countName")]
    [InlineData("""[{ "meter": "InputTokens", "amount": -1 }]""", "cannot be negative")]
    [InlineData("""[{ "meter": "Magic", "amount": 1 }]""", "Select what each price is for")]
    public async Task SetAIDeploymentPricing_RefusesAPriceTheEditorRefuses(string prices, string expected)
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetAIDeploymentPricingTool(), services, new()
        {
            ["deploymentId"] = "deployment-1",
            ["prices"] = JsonNode.Parse(prices),
        });

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.UpdatedDeployments);
    }

    [Fact]
    public async Task DeleteAIDeployment_SkipsSiteDefaultsAndConfigurationAndNamesTheProfiles()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");
        harness.AddDeployment("deployment-2", AIModelToolHarness.OpenAI, "gpt-old", "openai");
        harness.AddDeployment("deployment-3", AIModelToolHarness.OpenAI, "gpt-config", "openai", deployment => deployment.IsReadOnly = true);
        harness.Defaults.DefaultChatDeploymentName = "gpt-4o";
        harness.Profiles.Add(new AIProfile { ItemId = "support", Name = "support", DisplayText = "Support", ChatDeploymentName = "gpt-old" });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIDeploymentTool(), services, new() { ["deploymentIds"] = new[] { "deployment-1", "deployment-2", "deployment-3" } });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var deleted = Assert.Single(json.GetProperty("deleted").EnumerateArray());

        Assert.Equal("gpt-old", deleted.GetProperty("name").GetString());
        Assert.Equal("Support", Assert.Single(deleted.GetProperty("profilesThatNamedIt").EnumerateArray()).GetString());
        Assert.Equal(2, json.GetProperty("skipped").GetArrayLength());
        Assert.Equal(2, harness.Deployments.Count);
    }

    [Fact]
    public async Task ListAIDeployments_ShowsDefaultsProfilesAndPrices()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai", deployment => deployment.SetPricing("EUR", [new AIDeploymentPrice { Meter = "InputTokens", Amount = 2m, Per = 1_000_000 }]));
        harness.Defaults.DefaultChatDeploymentName = "gpt-4o";
        harness.Profiles.Add(new AIProfile { ItemId = "support", Name = "support", DisplayText = "Support", UtilityDeploymentName = "deployment-1" });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIDeploymentsTool(), services, new() { ["capability"] = "textGeneration" });

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        Assert.Equal("chat", Assert.Single(item.GetProperty("defaultFor").EnumerateArray()).GetString());
        Assert.Equal("Support", Assert.Single(item.GetProperty("profiles").EnumerateArray()).GetString());
        Assert.Equal("EUR", item.GetProperty("pricing").GetProperty("currency").GetString());
    }

    [Fact]
    public async Task ListAIVoices_SearchesTheDeploymentsLibrary()
    {
        // Arrange
        var harness = CreateVoiceHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIVoicesTool(), services, new() { ["query"] = "calm", ["gender"] = "female" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var voice = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("voice-calm", voice.GetProperty("id").GetString());
        Assert.Equal("voices", json.GetProperty("deploymentName").GetString());
        Assert.Equal(SpeechVoiceGender.Female, Assert.Single(harness.Voices.Searches).Gender);
    }

    [Fact]
    public async Task ListAIVoices_RefusesADeploymentWithoutALibrary()
    {
        // Arrange
        var harness = CreateVoiceHarness();
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIVoicesTool(), services, new() { ["deploymentName"] = "gpt-4o" });

        // Assert
        Assert.Contains("has no voice library", result, StringComparison.Ordinal);
        Assert.Empty(harness.Voices.Searches);
    }

    [Fact]
    public async Task DeleteAIVoice_DeletesAtTheProviderAndRelaysAProviderError()
    {
        // Arrange
        var harness = CreateVoiceHarness();

        using var services = harness.Build();

        // Act
        var deleted = await AgentToolTestContext.InvokeAsync(new DeleteAIVoiceTool(), services, new() { ["deploymentName"] = "voices", ["voiceId"] = "voice-bold" });
        harness.Voices.Failure = "Voice not found.";
        var failed = await AgentToolTestContext.InvokeAsync(new DeleteAIVoiceTool(), services, new() { ["deploymentName"] = "voices", ["voiceId"] = "missing" });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(deleted).GetProperty("deleted").GetBoolean());
        Assert.Equal("voice-bold", Assert.Single(harness.Voices.Deleted));
        Assert.Equal("The provider did not delete the voice: Voice not found.", failed);
    }

    private static AIModelToolHarness CreateVoiceHarness()
    {
        var harness = new AIModelToolHarness();
        harness.AddDeployment("speech-1", AIModelToolHarness.ElevenLabs, "voices", configure: deployment => deployment.Put(new AIDeploymentMetadata { Features = ["textToSpeech"] }));

        return harness;
    }
}
