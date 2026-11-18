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
    public async Task SaveAIProviderConnection_CreatesAManagedIdentityConnectionAndReloadsTheSite()
    {
        // Arrange
        var harness = new AIModelToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProviderConnectionTool(), services, new()
        {
            ["provider"] = "azure",
            ["name"] = "azure-eu",
            ["title"] = "Azure EU",
            ["endpoint"] = "https://contoso-eu.openai.azure.com/",
            ["authenticationType"] = "ManagedIdentity",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var connection = Assert.Single(harness.Connections);
        var payload = Assert.Single(harness.ConnectionPayloads);

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.True(json.GetProperty("siteReloads").GetBoolean());
        Assert.Contains("can end this chat", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal("azure-eu", connection.Name);
        Assert.Equal(AIModelToolHarness.Azure, connection.Source);
        Assert.Equal("ManagedIdentity", payload["AuthenticationType"].GetValue<string>());
        Assert.Null(payload["ApiKey"]);
        Assert.Equal(1, harness.ReleaseRequests);
    }

    [Fact]
    public async Task SaveAIProviderConnection_ANewConnectionThatNeedsAKey_IsLeftToTheAdminPage()
    {
        // Arrange
        var harness = new AIModelToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProviderConnectionTool(), services, new()
        {
            ["provider"] = AIModelToolHarness.OpenAI,
            ["name"] = "openai",
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains("needs an API key", result, StringComparison.Ordinal);
        Assert.Empty(harness.Connections);
        Assert.Equal(0, harness.ReleaseRequests);
    }

    [Fact]
    public async Task SaveAIProviderConnection_RefusesAKeyArgumentAndKeepsTheStoredKey()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProviderConnectionTool(), services, new()
        {
            ["connectionId"] = "connection-1",
            ["apiKey"] = "sk-new-key",
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains("never taken", result, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-new-key", result, StringComparison.Ordinal);
        Assert.Empty(harness.ConnectionPayloads);
        Assert.Contains("CipherText", JsonSerializer.Serialize(harness.Connections[0].Properties), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAIProviderConnection_AnEndpointChangeResendsTheStoredSignInFields()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.Azure, "azure", protectedKey: null, configure: connection =>
        {
            var metadata = (JsonObject)connection.Properties["AzureConnectionMetadata"];
            metadata["AuthenticationType"] = "ManagedIdentity";
            metadata["IdentityId"] = "identity-1";
        });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProviderConnectionTool(), services, new()
        {
            ["connectionId"] = "connection-1",
            ["endpoint"] = "https://contoso-eu.openai.azure.com/",
        });

        // Assert
        var payload = Assert.Single(harness.ConnectionPayloads);
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("updated").GetBoolean());
        Assert.Equal("https://contoso-eu.openai.azure.com/", payload["Endpoint"].GetValue<string>());
        Assert.Equal("ManagedIdentity", payload["AuthenticationType"].GetValue<string>());
        Assert.Equal("identity-1", payload["IdentityId"].GetValue<string>());
        Assert.Null(payload["Name"]);
        Assert.Equal("identity-1", json.GetProperty("identityId").GetString());
        Assert.Equal(1, harness.ReleaseRequests);
    }

    [Fact]
    public async Task SaveAIProviderConnection_RefusesAConnectionFromConfiguration()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai", configure: connection => connection.IsReadOnly = true);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProviderConnectionTool(), services, new()
        {
            ["connectionId"] = "connection-1",
            ["title"] = "Changed",
        });

        // Assert
        Assert.Contains("defined in configuration", result, StringComparison.Ordinal);
        Assert.Empty(harness.ConnectionPayloads);
    }

    [Fact]
    public async Task SaveAndDeleteAIProviderConnection_InsideATurn_LeaveTheRunningAssistantsConnection()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "openai");
        var assistant = new AIProfile { ItemId = "assistant", Name = "assistant", ChatDeploymentName = "gpt-4o" };
        harness.Profiles.Add(assistant);

        using var services = harness.Build();
        using var turn = AIConfigurationToolHarness.BeginTurn(assistant);

        // Act
        var saved = await AgentToolTestContext.InvokeAsync(new SaveAIProviderConnectionTool(), services, new() { ["connectionId"] = "connection-1", ["title"] = "Changed" });
        var deleted = await AgentToolTestContext.InvokeAsync(new DeleteAIProviderConnectionTool(), services, new() { ["connectionIds"] = new[] { "connection-1" } });

        // Assert
        Assert.StartsWith("Refused", saved, StringComparison.Ordinal);
        Assert.Empty(AgentToolTestContext.ReadJson(deleted).GetProperty("deleted").EnumerateArray());
        Assert.Single(harness.Connections);
        Assert.Equal(0, harness.ReleaseRequests);
    }

    [Fact]
    public async Task DeleteAIProviderConnection_NamesTheDeploymentsThatStopWorking()
    {
        // Arrange
        var harness = new AIModelToolHarness();
        harness.AddConnection("connection-1", AIModelToolHarness.OpenAI, "openai");
        harness.AddDeployment("deployment-1", AIModelToolHarness.OpenAI, "gpt-4o", "connection-1");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIProviderConnectionTool(), services, new() { ["connectionIds"] = new[] { "connection-1", "missing" } });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var deleted = Assert.Single(json.GetProperty("deleted").EnumerateArray());

        Assert.Equal("gpt-4o", Assert.Single(deleted.GetProperty("deploymentsThatStopWorking").EnumerateArray()).GetString());
        Assert.Single(json.GetProperty("skipped").EnumerateArray());
        Assert.Empty(harness.Connections);
        Assert.Equal(1, harness.ReleaseRequests);
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
    public async Task CloneAIVoice_SendsTheMediaRecordingToTheProvider()
    {
        // Arrange
        var harness = CreateVoiceHarness();
        harness.Media.AddFile("voices/ceo.mp3", [1, 2, 3]);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CloneAIVoiceTool(), services, new()
        {
            ["deploymentName"] = "voices",
            ["name"] = "CEO",
            ["language"] = "en",
            ["mediaPath"] = "voices/ceo.mp3",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var sample = Assert.Single(Assert.Single(harness.Voices.Clones).Samples);

        Assert.Equal("cloned-1", json.GetProperty("voiceId").GetString());
        Assert.Equal([1, 2, 3], sample.Content);
        Assert.Equal("audio/mpeg", sample.ContentType);
    }

    [Fact]
    public async Task CloneAIVoice_RefusesARecordingOverTenMegabytes()
    {
        // Arrange
        var harness = CreateVoiceHarness();
        harness.Media.AddFile("voices/long.wav", new byte[CloneAIVoiceTool.MaxSampleBytes + 1]);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CloneAIVoiceTool(), services, new()
        {
            ["deploymentName"] = "voices",
            ["name"] = "Long",
            ["mediaPath"] = "voices/long.wav",
        });

        // Assert
        Assert.StartsWith("Nothing was cloned", result, StringComparison.Ordinal);
        Assert.Empty(harness.Voices.Clones);
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
