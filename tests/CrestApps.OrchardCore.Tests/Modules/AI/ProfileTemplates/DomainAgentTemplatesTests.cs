using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Parsing;
using CrestApps.OrchardCore.AI.Agent.Analytics;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Agent.Quality;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Calendars.AI;
using CrestApps.OrchardCore.Scheduling.AI;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

/// <summary>
/// Parses the domain agent starting points (content, workflows, AI operations, calendar) the way the module
/// template provider does, then builds a profile from each. A tool renamed in code, or a front-matter mistake,
/// shows up here instead of as an agent that silently runs without the tools it needs.
/// </summary>
public sealed class DomainAgentTemplatesTests
{
    private const string AgentModule = "CrestApps.OrchardCore.AI.Agent";
    private const string CalendarsModule = "CrestApps.OrchardCore.Calendars";

    private static readonly Dictionary<string, (string Module, string[] Tools)> _agents = new()
    {
        ["content-agent"] = (AgentModule,
        [
            SearchForContentsTool.TheName,
            GetContentTool.TheName,
            GetSampleContentItemForContentTypeTool.TheName,
            CreateOrUpdateContentTool.TheName,
            CloneContentTool.TheName,
            PublishContentTool.TheName,
            UnpublishContentTool.TheName,
            DeleteContentTool.TheName,
            GetContentItemLinkTool.TheName,
            ListContentTypesDefinitionsTool.TheName,
            GetContentTypeDefinitionsTool.TheName,
            ListContentPartsDefinitionsTool.TheName,
            GetContentPartDefinitionsTool.TheName,
            ListContentFieldsTool.TheName,
            CreateOrUpdateContentTypeDefinitionsTool.TheName,
            RemoveContentTypeDefinitionsTool.TheName,
            RemoveContentPartDefinitionsTool.TheName,
        ]),
        ["workflow-agent"] = (AgentModule,
        [
            ListWorkflowTypesTool.TheName,
            GetWorkflowTypesTool.TheName,
            ListWorkflowActivitiesTool.TheName,
            CreateOrUpdateWorkflowTool.TheName,
        ]),
        ["ai-operations-agent"] = (AgentModule,
        [
            ListAIProfilesTool.TheName,
            ViewAIProfileTool.TheName,
            ListAIConversationsTool.TheName,
            ViewAIConversationTool.TheName,
            GetAIContactMemoryTool.TheName,
            SummarizeAIQualityTool.TheName,
            ListAITestCasesTool.TheName,
            RunAITestsTool.TheName,
            ListAIAlertsTool.TheName,
            QueryChatSessionMetricsTool.TheName,
        ]),
        ["calendar-agent"] = (CalendarsModule,
        [
            ListCalendarsTool.ToolName,
            FindCalendarEventsTool.ToolName,
            FindFreeTimeTool.ToolName,
            CreateCalendarEventTool.ToolName,
            MoveCalendarEventTool.ToolName,
            CancelCalendarEventTool.ToolName,
            ListAppointmentTypesTool.ToolName,
            FindAppointmentSlotsTool.ToolName,
            BookAppointmentTool.ToolName,
            ChangeAppointmentTool.ToolName,
        ]),
    };

    public static TheoryData<string> TemplateIds => [.. _agents.Keys];

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Template_ShouldParseAsAListedOnDemandAgentThatUsesItsTools(string id)
    {
        // Arrange
        var (module, _) = _agents[id];

        // Act
        var template = ParseTemplate(module, id);

        // Assert
        Assert.Equal(AITemplateSources.Profile, template.Source);
        Assert.True(template.IsListable);
        Assert.False(string.IsNullOrWhiteSpace(template.DisplayText));
        Assert.False(string.IsNullOrWhiteSpace(template.Description));

        var metadata = template.GetOrCreate<ProfileTemplateMetadata>();
        Assert.Equal(AIProfileType.Agent, metadata.ProfileType);
        Assert.False(string.IsNullOrWhiteSpace(metadata.Description));
        Assert.False(string.IsNullOrWhiteSpace(metadata.SystemMessage));

        // Always available would put the agent, and through it these tools, in every assistant on the site.
        Assert.Equal(AgentAvailability.OnDemand, metadata.AgentAvailability);

        // Without this the agent runs with no tools whenever another assistant calls it.
        Assert.True(metadata.AllowToolInvocation);
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Template_ShouldTickExactlyTheToolsOfItsArea(string id)
    {
        // Arrange
        var (module, expectedTools) = _agents[id];

        // Act
        var toolNames = ParseTemplate(module, id).GetOrCreate<ProfileTemplateMetadata>().ToolNames;

        // Assert
        Assert.Equal(expectedTools.Order(StringComparer.Ordinal), toolNames.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Template_ShouldNotTickSiteAdministrationTools(string id)
    {
        // Arrange
        var (module, _) = _agents[id];

        // Act
        var toolNames = ParseTemplate(module, id).GetOrCreate<ProfileTemplateMetadata>().ToolNames;

        // Assert

        // The tools do not check the caller's own permissions, so tenant, feature, recipe and settings tools stay
        // out of ready-made agents until they do.
        Assert.DoesNotContain(toolNames, tool => tool.EndsWith("Tenant", StringComparison.Ordinal));
        Assert.DoesNotContain(toolNames, tool => tool.Contains("SiteFeature", StringComparison.Ordinal));
        Assert.DoesNotContain(toolNames, tool => tool.Contains("Recipe", StringComparison.Ordinal) && !tool.Contains("ContentType", StringComparison.Ordinal));
        Assert.DoesNotContain("applySiteSettings", toolNames);
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Template_ShouldNeedTheFeaturesThatProvideItsTools(string id)
    {
        // Arrange
        var (module, _) = _agents[id];

        // Act
        var template = ParseTemplate(module, id);

        // Assert
        Assert.True(template.TryGet<ProfileScenarioMetadata>(out var scenario));
        Assert.False(scenario.Featured);
        Assert.StartsWith("fa-solid fa-", scenario.Icon);
        Assert.Contains(module, scenario.RequiresFeatures);
    }

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public void Template_ShouldBuildAnAgentProfileThatRunsTheTickedTools(string id)
    {
        // Arrange
        var (module, expectedTools) = _agents[id];
        var template = ParseTemplate(module, id);
        var profile = new AIProfile();

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        Assert.Equal(AIProfileType.Agent, profile.Type);
        Assert.False(string.IsNullOrWhiteSpace(profile.Description));

        var agentMetadata = profile.GetOrCreate<AgentMetadata>();
        Assert.Equal(AgentAvailability.OnDemand, agentMetadata.Availability);
        Assert.True(agentMetadata.AllowToolInvocation);

        var toolMetadata = profile.GetOrCreate<FunctionInvocationMetadata>();
        Assert.Equal(expectedTools.Order(StringComparer.Ordinal), toolMetadata.Names.Order(StringComparer.Ordinal));

        Assert.False(profile.Has<ProfileScenarioMetadata>());
    }

    private static AIProfileTemplate ParseTemplate(string module, string id)
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", module, "Templates", "Profiles", id + ".md");
        var parseResult = new DefaultMarkdownTemplateParser().Parse(File.ReadAllText(path));
        var template = AIProfileTemplateParser.Parse(id, parseResult);

        ProfileScenarioMetadataReader.Apply(template, parseResult.Metadata);
        ProfileTemplateDefaultsReader.Apply(template, parseResult.Metadata);

        return template;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Unable to locate the repository root.");
    }
}
