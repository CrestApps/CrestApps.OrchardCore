using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Parsing;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AI;

/// <summary>
/// The ready-made "Contact center manager" profile: it ticks exactly the Contact Center tools, needs the feature that
/// provides them, and tells the model to call them and answer only from what they return.
/// </summary>
public sealed class ContactCenterManagerTemplateTests
{
    private const string Id = "contact-center-manager";

    private static readonly string[] _tools =
    [
        ExplainAgentNoCallsTool.ToolName,
        QueueInventoryTool.ToolName,
        DispatchDiagnosticsTool.ToolName,
        DecisionLogTool.ToolName,
        ExplainActivityTool.ToolName,
        ListConfigurationTool.ToolName,
        AgentRosterTool.ToolName,
        StuckWorkTool.ToolName,
    ];

    [Fact]
    public void Template_ParsesAsAListedChatProfileThatTicksExactlyTheContactCenterTools()
    {
        // Act
        var template = ParseTemplate();
        var metadata = template.GetOrCreate<ProfileTemplateMetadata>();

        // Assert
        Assert.Equal(AITemplateSources.Profile, template.Source);
        Assert.True(template.IsListable);
        Assert.Equal(AIProfileType.Chat, metadata.ProfileType);
        Assert.Equal(_tools.Order(StringComparer.Ordinal), metadata.ToolNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Template_NeedsTheFeatureThatProvidesItsTools()
    {
        // Act
        var template = ParseTemplate();

        // Assert
        Assert.True(template.TryGet<ProfileScenarioMetadata>(out var scenario));
        Assert.Contains(ContactCenterConstants.Feature.AITools, scenario.RequiresFeatures);
    }

    [Fact]
    public void Template_TellsTheModelToCallEveryToolAndToAnswerOnlyFromTheirResults()
    {
        // Act
        var prompt = ParseTemplate().GetOrCreate<ProfileTemplateMetadata>().SystemMessage;

        // Assert
        foreach (var tool in _tools)
        {
            Assert.Contains(tool, prompt, StringComparison.Ordinal);
        }

        Assert.Contains("Answer only from what the tools return", prompt, StringComparison.Ordinal);
        Assert.Contains("Never guess", prompt, StringComparison.Ordinal);
        Assert.Contains("read only", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Template_BuildsAChatProfileWithTheToolsTicked()
    {
        // Arrange
        var profile = new AIProfile();

        // Act
        AIProfileTemplateApplicator.Apply(profile, ParseTemplate());

        // Assert
        Assert.Equal(AIProfileType.Chat, profile.Type);
        Assert.Equal(_tools.Order(StringComparer.Ordinal), profile.GetOrCreate<FunctionInvocationMetadata>().Names.Order(StringComparer.Ordinal));
    }

    private static AIProfileTemplate ParseTemplate()
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Templates", "Profiles", Id + ".md");
        var parseResult = new DefaultMarkdownTemplateParser().Parse(File.ReadAllText(path));
        var template = AIProfileTemplateParser.Parse(Id, parseResult);

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
