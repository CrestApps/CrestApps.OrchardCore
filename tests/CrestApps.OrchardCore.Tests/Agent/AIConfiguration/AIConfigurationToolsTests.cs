using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Personas;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Agent.ProfileTemplates;
using CrestApps.OrchardCore.AI.Agent.ToolInstances;

namespace CrestApps.OrchardCore.Tests.Agent.AIConfiguration;

/// <summary>
/// The main path and a failure path of each AI profile, template, persona and tool instance tool, on in-memory stores.
/// </summary>
public sealed class AIConfigurationToolsTests
{
    [Fact]
    public async Task ListAITools_ListsOnlyToolsTheCallerMayUse_WithTheVisitorFlag()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.DeniedTools.Add(AIConfigurationToolHarness.PowerfulToolName);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIToolsTool(), services, new() { ["kind"] = "tool" });

        // Assert
        var items = AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray().ToList();

        var item = Assert.Single(items);
        Assert.Equal(AIConfigurationToolHarness.SafeToolName, item.GetProperty("name").GetString());
        Assert.True(item.GetProperty("safeForVisitors").GetBoolean());
    }

    [Fact]
    public async Task ListAITools_RefusesAnUnknownKind()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIToolsTool(), services, new() { ["kind"] = "widgets" });

        // Assert
        Assert.StartsWith("Nothing was listed", result);
    }

    [Fact]
    public async Task SaveAIProfile_CreatesAProfileWithANote()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProfileTool(), services, new()
        {
            ["name"] = "returns-helper",
            ["title"] = "Returns Helper",
            ["instructions"] = "Answer questions about the return policy in a friendly tone.",
        });

        // Assert
        Assert.Contains("\"created\":true", result);

        var profile = Assert.Single(harness.Profiles.All);
        Assert.Equal("Returns Helper", profile.DisplayText);
        Assert.Equal("Answer questions about the return policy in a friendly tone.", profile.GetOrCreate<AIProfileMetadata>().SystemMessage);
        Assert.Contains(AgentToolTestContext.UserName, profile.GetOrCreate<AIProfileVersionMetadata>().PendingNote);
        Assert.Equal(AgentToolTestContext.UserId, profile.OwnerId);
    }

    [Fact]
    public async Task SaveAIProfile_RefusesANewProfileWithoutATitle()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProfileTool(), services, new() { ["name"] = "nameless" });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Empty(harness.Profiles.All);
    }

    [Fact]
    public async Task SaveAIProfile_RefusesNewInstructionsOnALockedProfile()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.AddProfile("locked", "locked", configure: profile => profile.WithSettings(new AIProfileSettings { LockSystemMessage = true }));

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProfileTool(), services, new() { ["profileId"] = "locked", ["instructions"] = "New rules." });

        // Assert
        Assert.Contains("locked", result);
        Assert.Empty(harness.Profiles.Updated);
    }

    [Fact]
    public async Task SaveAIProfile_RefusesAPersonaThatDoesNotExist()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.AddProfile("support", "support");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProfileTool(), services, new() { ["profileId"] = "support", ["personaName"] = "ghost" });

        // Assert
        Assert.Contains("no persona is named 'ghost'", result);
        Assert.Empty(harness.Profiles.Updated);
    }

    [Fact]
    public async Task SetAIProfileCapabilities_RefusesAnUnknownTool()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.AddProfile("support", "support");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetAIProfileCapabilitiesTool(), services, new() { ["profileId"] = "support", ["toolNames"] = new[] { "noSuchTool" } });

        // Assert
        Assert.Contains("noSuchTool", result);
        Assert.Empty(harness.Profiles.Updated);
    }

    [Fact]
    public async Task CreateAIProfileFromTemplate_CreatesTheProfileWithTheTemplatesValues()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        var template = new AIProfileTemplate { ItemId = "template-1", Name = "starter", Source = "Profile", DisplayText = "Starter" };
        template.Put(new ProfileTemplateMetadata { ProfileType = AIProfileType.Chat, WelcomeMessage = "Welcome!", ToolNames = [AIConfigurationToolHarness.SafeToolName] });
        harness.Templates.Add(template);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CreateAIProfileFromTemplateTool(), services, new() { ["templateId"] = "template-1", ["title"] = "Shop Helper", ["name"] = "shop-helper" });

        // Assert
        Assert.Contains("\"created\":true", result);

        var profile = Assert.Single(harness.Profiles.All);
        Assert.Equal("shop-helper", profile.Name);
        Assert.Equal("Shop Helper", profile.DisplayText);
        Assert.Equal("Welcome!", profile.WelcomeMessage);
    }

    [Fact]
    public async Task CreateAIProfileFromTemplate_RefusesASystemPromptTemplate()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.Templates.Add(new AIProfileTemplate { ItemId = "style-1", Name = "style", Source = "SystemPrompt" });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CreateAIProfileFromTemplateTool(), services, new() { ["templateId"] = "style-1", ["title"] = "X", ["name"] = "x" });

        // Assert
        Assert.StartsWith("Nothing was created", result);
        Assert.Empty(harness.Profiles.All);
    }

    [Fact]
    public async Task ListAIProfileVersions_ListsTheVersionsAndTheChangedSettings()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.AddProfile("support", "support", configure: profile =>
        {
            profile.WelcomeMessage = "Hello now";
            profile.Put(new AIProfileVersionMetadata { Version = 2 });
        });
        harness.Versions.Add(new AIProfileVersion { ItemId = "version-1", ProfileId = "support", Version = 1, Author = "admin", Note = "Created", Snapshot = new AIProfile { ItemId = "support", Name = "support", DisplayText = "support", WelcomeMessage = "Hello then" } });
        harness.Versions.Add(new AIProfileVersion { ItemId = "version-2", ProfileId = "support", Version = 2, Author = "admin", Snapshot = new AIProfile { ItemId = "support", Name = "support", DisplayText = "support", WelcomeMessage = "Hello now" } });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIProfileVersionsTool(), services, new() { ["profileId"] = "support", ["versionId"] = "version-1" });

        // Assert
        var root = AgentToolTestContext.ReadJson(result);

        Assert.Equal(2, root.GetProperty("total").GetInt32());
        Assert.Equal(2, root.GetProperty("currentVersion").GetInt32());
        Assert.Contains("Hello then", result);
        Assert.Contains("Hello now", result);
    }

    [Fact]
    public async Task ListAIProfileVersions_RefusesAVersionOfAnotherProfile()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.AddProfile("support", "support");
        harness.Versions.Add(new AIProfileVersion { ItemId = "version-x", ProfileId = "other", Version = 1, Snapshot = new AIProfile() });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIProfileVersionsTool(), services, new() { ["profileId"] = "support", ["versionId"] = "version-x" });

        // Assert
        Assert.Contains("has no version", result);
    }

    [Fact]
    public async Task RestoreAIProfileVersion_RestoresAndNotesIt()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.AddProfile("support", "support", configure: profile => profile.WelcomeMessage = "Now");
        harness.Versions.Add(new AIProfileVersion { ItemId = "version-1", ProfileId = "support", Version = 1, Snapshot = new AIProfile { ItemId = "support", Name = "support", DisplayText = "support", WelcomeMessage = "Then" } });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RestoreAIProfileVersionTool(), services, new() { ["profileId"] = "support", ["versionId"] = "version-1" });

        // Assert
        Assert.Contains("\"restored\":true", result);

        var profile = Assert.Single(harness.Profiles.Updated);
        Assert.Equal("Then", profile.WelcomeMessage);
        Assert.Contains("Restored from version 1", profile.GetOrCreate<AIProfileVersionMetadata>().PendingNote);
    }

    [Fact]
    public async Task TestAIProfile_SendsTheMessageWithToolsTurnedOff()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.AddProfile("support", "support");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new TestAIProfileTool(), services, new() { ["profileId"] = "support", ["message"] = "Hi" });

        // Assert
        Assert.Contains("Hello from the model.", result);
        Assert.True(Assert.Single(harness.Completions).DisableTools);
    }

    [Fact]
    public async Task TestAIProfile_InsideATurn_LeavesTheRunningAssistantsDataSource()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.AddProfile("support", "support");
        var running = harness.AddProfile("running", "running");

        using var services = harness.Build();
        using var turn = AIConfigurationToolHarness.BeginTurn(running);
        CrestApps.Core.AI.Orchestration.AIInvocationScope.Current.DataSourceId = "running-source";

        // Act
        await AgentToolTestContext.InvokeAsync(new TestAIProfileTool(), services, new() { ["profileId"] = "support", ["message"] = "Hi" });

        // Assert
        Assert.Single(harness.Completions);
        Assert.Equal("running-source", CrestApps.Core.AI.Orchestration.AIInvocationScope.Current.DataSourceId);
    }

    [Fact]
    public async Task TestAIProfile_RefusesAnUnknownProfile()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new TestAIProfileTool(), services, new() { ["profileId"] = "ghost", ["message"] = "Hi" });

        // Assert
        Assert.Contains("No AI profile has the ID 'ghost'", result);
        Assert.Empty(harness.Completions);
    }

    [Fact]
    public async Task ListAIProfileTemplates_MarksShippedTemplates()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.Templates.Add(new AIProfileTemplate { ItemId = "stored", Name = "stored", Source = "SystemPrompt" });
        harness.ShippedTemplates.Add(new AIProfileTemplate { ItemId = "shipped", Name = "shipped", Source = "SystemPrompt" });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIProfileTemplatesTool(), services);

        // Assert
        var items = AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetString());

        Assert.False(items["stored"].GetProperty("isShipped").GetBoolean());
        Assert.True(items["shipped"].GetProperty("isShipped").GetBoolean());
    }

    [Fact]
    public async Task GetAIProfileTemplate_ReturnsTheInstructions()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        var template = new AIProfileTemplate { ItemId = "style-1", Name = "brief", Source = "SystemPrompt" };
        template.Put(new SystemPromptTemplateMetadata { SystemMessage = "Answer in two sentences." });
        harness.Templates.Add(template);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetAIProfileTemplateTool(), services, new() { ["templateId"] = "style-1" });

        // Assert
        Assert.Equal("Answer in two sentences.", AgentToolTestContext.ReadJson(result).GetProperty("instructions").GetString());
    }

    [Fact]
    public async Task SaveAIProfileTemplate_CreatesASpeakingStyleUnlisted()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProfileTemplateTool(), services, new()
        {
            ["source"] = "SystemPrompt",
            ["name"] = "plain-and-brief",
            ["title"] = "Plain and brief",
            ["category"] = AISpeakingStyles.Category,
            ["instructions"] = "Answer in two sentences.",
        });

        // Assert
        Assert.Contains("\"created\":true", result);

        var template = Assert.Single(harness.Templates);
        Assert.False(template.IsListable);
        Assert.Equal("Answer in two sentences.", template.GetOrCreate<SystemPromptTemplateMetadata>().SystemMessage);
    }

    [Fact]
    public async Task SaveAIProfileTemplate_RefusesAShippedTemplate()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.ShippedTemplates.Add(new AIProfileTemplate { ItemId = "shipped", Name = "shipped", Source = "SystemPrompt", DisplayText = "Shipped" });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIProfileTemplateTool(), services, new() { ["templateId"] = "shipped", ["title"] = "Mine" });

        // Assert
        Assert.Contains("shipped with a module", result);
    }

    [Fact]
    public async Task DeleteAIProfileTemplate_DeletesStoredAndSkipsShipped()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.Templates.Add(new AIProfileTemplate { ItemId = "stored", Name = "stored", Source = "SystemPrompt" });
        harness.ShippedTemplates.Add(new AIProfileTemplate { ItemId = "shipped", Name = "shipped", Source = "SystemPrompt" });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIProfileTemplateTool(), services, new() { ["templateIds"] = new[] { "stored", "shipped" } });

        // Assert
        Assert.Empty(harness.Templates);
        Assert.Contains("cannot be deleted", result);
    }

    [Fact]
    public async Task ListAIPersonas_CountsTheLinkedProfiles()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.Personas.Add(new AIPersona { ItemId = "persona-1", Name = "maya", DisplayText = "Maya", Instructions = "Be warm." });
        harness.AddProfile("support", "support", configure: profile => profile.Put(new AIPersonaMetadata { PersonaId = "maya" }));

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIPersonasTool(), services, new() { ["name"] = "maya" });

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        Assert.Equal(1, item.GetProperty("linkedProfileCount").GetInt32());
        Assert.Equal("Be warm.", item.GetProperty("instructions").GetString());
    }

    [Fact]
    public async Task SaveAIPersona_SavesAPersonaFileAsAStoredCopyWithTheSameName()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.PersonaFiles.Add(new AIPersona { ItemId = "file-maya", Name = "maya", Instructions = "Be warm.", Language = "en-GB" });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIPersonaTool(), services, new() { ["personaId"] = "file-maya", ["assistantName"] = "Maya from Support" });

        // Assert
        Assert.Contains("\"created\":true", result);

        var persona = Assert.Single(harness.Personas);
        Assert.Equal("maya", persona.Name);
        Assert.Equal("Maya from Support", persona.AssistantName);
        Assert.Equal("en-GB", persona.Language);
        Assert.Equal("Be warm.", persona.Instructions);
    }

    [Fact]
    public async Task SaveAIPersona_RefusesANewPersonaWithoutInstructions()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIPersonaTool(), services, new() { ["name"] = "maya" });

        // Assert
        Assert.Contains("instructions are required", result);
        Assert.Empty(harness.Personas);
    }

    [Fact]
    public async Task DeleteAIPersona_ReportsTheProfilesNowWithoutAPersona()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.Personas.Add(new AIPersona { ItemId = "persona-1", Name = "maya", Instructions = "Be warm." });
        harness.AddProfile("support", "support", configure: profile => profile.Put(new AIPersonaMetadata { PersonaId = "maya" }));
        harness.AddProfile("sales", "sales", configure: profile => profile.Put(new AIPersonaMetadata { PersonaId = "maya" }));

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIPersonaTool(), services, new() { ["personaIds"] = new[] { "persona-1" } });

        // Assert
        Assert.Empty(harness.Personas);
        Assert.Contains("\"profilesNowWithoutPersona\":2", result);
    }

    [Fact]
    public async Task ListAIToolInstances_ListsTheProfilesThatUseAnInstance()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.Instances.Add(new AIToolInstance { ItemId = "instance-1", Name = "orders", Source = "sitemap-documentation", Description = "Orders" });
        harness.AddProfile("support", "support", configure: profile => profile.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["orders"] }));

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIToolInstancesTool(), services);

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        Assert.Equal(1, item.GetProperty("usedByProfileCount").GetInt32());
    }

    [Fact]
    public async Task SaveAIToolInstance_CreatesAnInstanceOwnedByTheCaller()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIToolInstanceTool(), services, new()
        {
            ["source"] = "sitemap-documentation",
            ["name"] = "oc-docs",
            ["description"] = "Searches the Orchard Core documentation.",
            ["settings"] = new JsonObject { ["baseUrl"] = "https://docs.orchardcore.net", ["sitemapUrl"] = "https://docs.orchardcore.net/sitemap.xml" },
        });

        // Assert
        Assert.Contains("\"created\":true", result);

        var instance = Assert.Single(harness.Instances);
        Assert.Equal(AgentToolTestContext.UserId, instance.OwnerId);
        Assert.Contains("\"baseUrl\":\"https://docs.orchardcore.net\"", result);
    }

    [Fact]
    public async Task SaveAIToolInstance_RefusesAnUnknownSetting()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIToolInstanceTool(), services, new()
        {
            ["source"] = "sitemap-documentation",
            ["name"] = "oc-docs",
            ["description"] = "Docs",
            ["settings"] = new JsonObject { ["colour"] = "blue" },
        });

        // Assert
        Assert.Contains("'colour' is not a setting", result);
        Assert.Empty(harness.Instances);
    }

    [Fact]
    public async Task DeleteAIToolInstance_DeletesTheCallersInstance()
    {
        // Arrange
        var harness = new AIConfigurationToolHarness();
        harness.Instances.Add(new AIToolInstance { ItemId = "instance-1", Name = "orders", Source = "sitemap-documentation", OwnerId = AgentToolTestContext.UserId });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIToolInstanceTool(), services, new() { ["instanceIds"] = new[] { "instance-1", "ghost" } });

        // Assert
        Assert.Empty(harness.Instances);
        Assert.Contains("No tool instance has this ID", result);
    }
}
