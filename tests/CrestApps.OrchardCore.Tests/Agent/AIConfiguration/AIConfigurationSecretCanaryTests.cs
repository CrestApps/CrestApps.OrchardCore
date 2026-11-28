using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.Core.AI.Tooling.Instances.Documentation;
using CrestApps.OrchardCore.AI.Agent.Personas;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Agent.ProfileTemplates;
using CrestApps.OrchardCore.AI.Agent.ToolInstances;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Agent.AIConfiguration;

/// <summary>
/// No result of the AI profile, template, persona and tool instance tools carries a secret. Every secret field of the
/// fixtures holds a canary value, and none may appear in any result, errors included. Credentials are never accepted
/// either, and stored ones survive an update.
/// </summary>
public sealed class AIConfigurationSecretCanaryTests
{
    private static readonly string[] _canaries =
    [
        "CANARY-HTTP-APIKEY",
        "CANARY-HTTP-BEARER",
        "CANARY-HTTP-PASSWORD",
        "CANARY-HTTP-CLIENTSECRET",
        "CANARY-HTTP-HEADER",
        "CANARY-ACCESS-TOKEN",
        "CANARY-REFRESH-TOKEN",
        "CANARY-ALGOLIA-APIKEY",
        "CANARY-PROFILE-PROPERTY",
        "CANARY-SNAPSHOT-PROPERTY",
        "CANARY-TEMPLATE-PROPERTY",
        "CANARY-PERSONA-PROPERTY",
    ];

    public static TheoryData<string, Dictionary<string, object>> Calls => new()
    {
        { ListAIToolsTool.TheName, new() },
        { ListAIToolInstancesTool.TheName, new() { ["includeSources"] = true } },
        { SaveAIToolInstanceTool.TheName, new() { ["instanceId"] = "http-1", ["description"] = "Looks up orders", ["settings"] = new JsonObject { ["timeoutSeconds"] = 20 } } },
        { SaveAIToolInstanceTool.TheName, new() { ["instanceId"] = "http-1", ["settings"] = new JsonObject { ["apiKey"] = "new-key" } } },
        { SaveAIToolInstanceTool.TheName, new() { ["instanceId"] = "http-1", ["settings"] = new JsonObject { ["baseUrl"] = "https://elsewhere.example.com" } } },
        { SaveAIToolInstanceTool.TheName, new() { ["instanceId"] = "algolia-1", ["description"] = "Docs" } },
        { DeleteAIToolInstanceTool.TheName, new() { ["instanceIds"] = new[] { "algolia-1" } } },
        { ListAIProfileVersionsTool.TheName, new() { ["profileId"] = "support", ["versionId"] = "version-1", ["includeUnchanged"] = true } },
        { RestoreAIProfileVersionTool.TheName, new() { ["profileId"] = "support", ["versionId"] = "version-1" } },
        { SaveAIProfileTool.TheName, new() { ["profileId"] = "support", ["welcomeMessage"] = "Hi" } },
        { SetAIProfileCapabilitiesTool.TheName, new() { ["profileId"] = "support", ["toolInstanceNames"] = new[] { "orders" } } },
        { DeleteAIProfileTool.TheName, new() { ["profileId"] = "support" } },
        { DiagnoseAIProfileTool.TheName, new() { ["profileId"] = "support" } },
        { TestAIProfileTool.TheName, new() { ["profileId"] = "support", ["message"] = "Hi" } },
        { ListAIProfileTemplatesTool.TheName, new() },
        { ListAIProfileTemplatesTool.TheName, new() { ["id"] = "template-1" } },
        { SaveAIProfileTemplateTool.TheName, new() { ["templateId"] = "template-1", ["title"] = "Starter" } },
        { SaveAIProfileTool.TheName, new() { ["templateId"] = "template-1", ["title"] = "New", ["name"] = "new-bot" } },
        { DeleteAIProfileTemplateTool.TheName, new() { ["templateIds"] = new[] { "template-1" } } },
        { ListAIPersonasTool.TheName, new() { ["name"] = "maya" } },
        { SaveAIPersonaTool.TheName, new() { ["personaId"] = "persona-1", ["title"] = "Maya" } },
        { DeleteAIPersonaTool.TheName, new() { ["personaIds"] = new[] { "persona-1" } } },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task Result_NeverCarriesASecret(string toolName, Dictionary<string, object> arguments)
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
    public async Task ListAIToolInstances_ReportsStoredCredentialsAsFlagsAndHeadersByName()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIToolInstancesTool(), services);

        // Assert
        Assert.Contains("\"hasApiKey\":true", result);
        Assert.Contains("\"hasClientSecret\":true", result);
        Assert.Contains("\"hasAccessToken\":true", result);
        Assert.Contains("\"defaultHeaderNames\":[\"X-Auth\"]", result);
        Assert.Contains("\"baseUrl\":\"https://api.example.com\"", result);
    }

    [Fact]
    public async Task SaveAIToolInstance_RefusesACredential()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIToolInstanceTool(), services, new()
        {
            ["source"] = "http-api-request",
            ["name"] = "new-api",
            ["description"] = "Calls an API",
            ["settings"] = new JsonObject { ["baseUrl"] = "https://api.example.com", ["bearerToken"] = "typed-in-chat" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Contains("never taken from a chat", result);
        Assert.Equal(2, harness.Instances.Count);
    }

    [Fact]
    public async Task SaveAIToolInstance_KeepsStoredCredentialsWhenOtherSettingsChange()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIToolInstanceTool(), services, new()
        {
            ["instanceId"] = "http-1",
            ["settings"] = new JsonObject { ["timeoutSeconds"] = 20, ["httpMethod"] = "POST" },
        });

        // Assert
        Assert.Contains("\"updated\":true", result);

        var update = Assert.Single(harness.InstanceUpdates);
        Assert.DoesNotContain("ApiKey", update.ToJsonString(), StringComparison.Ordinal);

        var stored = harness.Instances.Single(instance => instance.ItemId == "http-1").GetOrCreate<HttpApiRequestToolSettings>();
        Assert.Equal("CANARY-HTTP-APIKEY", stored.ApiKey);
        Assert.Equal("CANARY-HTTP-CLIENTSECRET", stored.ClientSecret);
        Assert.Equal(20, stored.TimeoutSeconds);
        Assert.Equal("POST", stored.HttpMethod);
    }

    [Fact]
    public async Task SaveAIToolInstance_RefusesToMoveAStoredCredentialToAnotherAddress()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIToolInstanceTool(), services, new()
        {
            ["instanceId"] = "http-1",
            ["settings"] = new JsonObject { ["tokenEndpoint"] = "https://attacker.example.com/token" },
        });

        // Assert
        Assert.Contains("stored credential", result);
        Assert.Empty(harness.InstanceUpdates);
    }

    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("file:///etc/passwd")]
    public async Task SaveAIToolInstance_RefusesAnInternalOrNonWebAddress(string address)
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIToolInstanceTool(), services, new()
        {
            ["source"] = "http-api-request",
            ["name"] = "new-api",
            ["description"] = "Calls an API",
            ["settings"] = new JsonObject { ["baseUrl"] = address },
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Equal(2, harness.Instances.Count);
    }

    private static AIConfigurationToolHarness CreateHarness()
    {
        var harness = new AIConfigurationToolHarness
        {
            ModelReply = "A reply.",
        };

        var http = new AIToolInstance { ItemId = "http-1", Name = "orders", Source = "http-api-request", Description = "Orders" };
        http.Put(new HttpApiRequestToolSettings
        {
            BaseUrl = "https://api.example.com",
            ApiKey = "CANARY-HTTP-APIKEY",
            BearerToken = "CANARY-HTTP-BEARER",
            Password = "CANARY-HTTP-PASSWORD",
            ClientSecret = "CANARY-HTTP-CLIENTSECRET",
            DefaultHeaders = new Dictionary<string, string> { ["X-Auth"] = "CANARY-HTTP-HEADER" },
        });
        http.Put(new HttpApiRequestTokenState { AccessToken = "CANARY-ACCESS-TOKEN", RefreshToken = "CANARY-REFRESH-TOKEN" });

        var algolia = new AIToolInstance { ItemId = "algolia-1", Name = "docs", Source = "algolia-documentation", Description = "Docs" };
        algolia.Put(new AlgoliaDocumentationToolSettings { ApplicationId = "APP", ApiKey = "CANARY-ALGOLIA-APIKEY", IndexName = "docs" });

        harness.Instances.Add(http);
        harness.Instances.Add(algolia);

        harness.AddProfile("support", "support", configure: profile =>
        {
            profile.Properties["SecretThing"] = new JsonObject { ["ApiKey"] = "CANARY-PROFILE-PROPERTY" };
            profile.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["orders"] });
        });

        var snapshot = new AIProfile { ItemId = "support", Name = "support", DisplayText = "Support" };
        snapshot.Properties["SecretThing"] = new JsonObject { ["ApiKey"] = "CANARY-SNAPSHOT-PROPERTY", ["Other"] = "CANARY-SNAPSHOT-PROPERTY" };
        harness.Versions.Add(new AIProfileVersion { ItemId = "version-1", ProfileId = "support", Version = 1, Snapshot = snapshot });

        var template = new AIProfileTemplate { ItemId = "template-1", Name = "starter", Source = "Profile", DisplayText = "Starter" };
        template.Put(new ProfileTemplateMetadata { ProfileType = AIProfileType.Chat });
        template.Properties["SecretThing"] = new JsonObject { ["ApiKey"] = "CANARY-TEMPLATE-PROPERTY" };
        harness.Templates.Add(template);

        var persona = new AIPersona { ItemId = "persona-1", Name = "maya", Instructions = "Be warm." };
        persona.Properties["SecretThing"] = new JsonObject { ["ApiKey"] = "CANARY-PERSONA-PROPERTY" };
        harness.Personas.Add(persona);

        return harness;
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ListAIToolsTool.TheName => new ListAIToolsTool(),
            SaveAIProfileTool.TheName => new SaveAIProfileTool(),
            SetAIProfileCapabilitiesTool.TheName => new SetAIProfileCapabilitiesTool(),
            DeleteAIProfileTool.TheName => new DeleteAIProfileTool(),
            DiagnoseAIProfileTool.TheName => new DiagnoseAIProfileTool(),
            ListAIProfileVersionsTool.TheName => new ListAIProfileVersionsTool(),
            RestoreAIProfileVersionTool.TheName => new RestoreAIProfileVersionTool(),
            TestAIProfileTool.TheName => new TestAIProfileTool(),
            ListAIProfileTemplatesTool.TheName => new ListAIProfileTemplatesTool(),
            SaveAIProfileTemplateTool.TheName => new SaveAIProfileTemplateTool(),
            DeleteAIProfileTemplateTool.TheName => new DeleteAIProfileTemplateTool(),
            ListAIPersonasTool.TheName => new ListAIPersonasTool(),
            SaveAIPersonaTool.TheName => new SaveAIPersonaTool(),
            DeleteAIPersonaTool.TheName => new DeleteAIPersonaTool(),
            ListAIToolInstancesTool.TheName => new ListAIToolInstancesTool(),
            SaveAIToolInstanceTool.TheName => new SaveAIToolInstanceTool(),
            DeleteAIToolInstanceTool.TheName => new DeleteAIToolInstanceTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
