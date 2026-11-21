using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed class SurveyTemplateProviderTests
{
    [Fact]
    public void GetTemplates_EveryTemplateBuildsADefinitionWithAPageAndADefaultEnding()
    {
        // Arrange
        var registry = AuthoringSurveyHarness.CreateRegistry();
        var provider = new SurveyTemplateProvider(new SurveyDraftEditor(registry, new AuthoringLocalizer<SurveyDraftEditor>()), new AuthoringLocalizer<SurveyTemplateProvider>());

        // Act
        var templates = provider.GetTemplates().ToList();

        // Assert
        Assert.Equal(8, templates.Count);

        foreach (var template in templates)
        {
            var definition = provider.Build(template.Name);
            Assert.NotNull(definition);
            Assert.NotEmpty(definition.Pages);
            Assert.Single(definition.Endings, ending => ending.IsDefault);
        }
    }

    [Fact]
    public void Build_UnknownTemplate_ReturnsNull()
    {
        var provider = new SurveyTemplateProvider(new SurveyDraftEditor(AuthoringSurveyHarness.CreateRegistry(), new AuthoringLocalizer<SurveyDraftEditor>()), new AuthoringLocalizer<SurveyTemplateProvider>());

        Assert.Null(provider.Build("nope"));
    }

    [Fact]
    public void Build_Quiz_TurnsScoringOn()
    {
        var provider = new SurveyTemplateProvider(new SurveyDraftEditor(AuthoringSurveyHarness.CreateRegistry(), new AuthoringLocalizer<SurveyDraftEditor>()), new AuthoringLocalizer<SurveyTemplateProvider>());

        var definition = provider.Build(SurveyTemplateProvider.Quiz);

        Assert.True(definition.Scoring.Enabled);
        Assert.Equal(70, definition.Scoring.PassMarkPercent);
    }
}
