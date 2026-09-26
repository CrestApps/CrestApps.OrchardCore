using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Parsing;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

/// <summary>
/// Parses the starter scenarios shipped with the AI module the way the module template provider does, so a
/// front-matter mistake shows up here rather than as a missing or half-filled card in the gallery.
/// </summary>
public sealed class StarterScenarioTemplatesTests
{
    [Theory]
    [InlineData("website-assistant", AIProfileType.Chat, "Talk to people")]
    [InlineData("docs-assistant", AIProfileType.Chat, "Talk to people")]
    [InlineData("guided-intake", AIProfileType.Chat, "Talk to people")]
    [InlineData("background-summarizer", AIProfileType.Utility, "Do work in the background")]
    public void StarterScenario_ShouldParseAsAFeaturedScenario(string id, AIProfileType expectedType, string expectedCategory)
    {
        // Act
        var template = ParseScenario(id);

        // Assert
        Assert.True(template.IsListable);
        Assert.Equal(expectedCategory, template.Category);
        Assert.False(string.IsNullOrWhiteSpace(template.DisplayText));
        Assert.False(string.IsNullOrWhiteSpace(template.Description));

        Assert.True(template.TryGet<ProfileScenarioMetadata>(out var scenario));
        Assert.True(scenario.Featured);
        Assert.StartsWith("fa-", scenario.Icon);
        Assert.True(scenario.Order > 0);

        var profileMetadata = template.GetOrCreate<ProfileTemplateMetadata>();
        Assert.Equal(expectedType, profileMetadata.ProfileType);
        Assert.False(string.IsNullOrWhiteSpace(profileMetadata.SystemMessage));
    }

    [Fact]
    public void DocsAssistant_ShouldRequireChatAndProfileDocuments()
    {
        // Act
        var scenario = ParseScenario("docs-assistant").GetOrCreate<ProfileScenarioMetadata>();

        // Assert
        Assert.Equal(["CrestApps.OrchardCore.AI.Chat", "CrestApps.OrchardCore.AI.Documents.Profiles"], scenario.RequiresFeatures);
    }

    [Theory]
    [InlineData("website-assistant")]
    [InlineData("docs-assistant")]
    [InlineData("guided-intake")]
    public void ChatScenario_ShouldGreetAndGenerateTitles(string id)
    {
        // Act
        var metadata = ParseScenario(id).GetOrCreate<ProfileTemplateMetadata>();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(metadata.WelcomeMessage));
        Assert.Equal(AISessionTitleType.Generated, metadata.TitleType);
    }

    private static AIProfileTemplate ParseScenario(string id)
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.AI", "Templates", "Profiles", id + ".md");
        var parseResult = new DefaultMarkdownTemplateParser().Parse(File.ReadAllText(path));
        var template = AIProfileTemplateParser.Parse(id, parseResult);

        ProfileScenarioMetadataReader.Apply(template, parseResult.Metadata);

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
