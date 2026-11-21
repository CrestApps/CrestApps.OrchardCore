using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using OrchardCore;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

/// <summary>
/// Survey templates are records: the built-in provider offers a blank survey and every stored template, and new
/// surveys copy a template's definition with new ids. Surveys and Learning seed theirs with recipes.
/// </summary>
public sealed class SurveyTemplateProviderTests : IAsyncLifetime
{
    private AuthoringSurveyHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await AuthoringSurveyHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task GetTemplatesAsync_NoStoredTemplates_OffersOnlyTheBlankSurvey()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);

        // Act
        var templates = (await services.Templates.GetTemplatesAsync(cancellationToken)).ToList();
        var blank = await services.Templates.BuildAsync(SurveyTemplateNames.Blank, cancellationToken);

        // Assert
        var only = Assert.Single(templates);
        Assert.Equal(SurveyTemplateNames.Blank, only.Name);
        Assert.Single(blank.Pages);
        Assert.Single(blank.Endings, ending => ending.IsDefault);
    }

    [Fact]
    public async Task GetTemplatesAsync_StoredTemplates_AreOfferedByPositionAfterTheBlankSurvey()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);

        await services.TemplateStore.CreateAsync(Template("second", "Second", position: 20), cancellationToken);
        await services.TemplateStore.CreateAsync(Template("first", "First", position: 10, kind: SurveyKinds.Quiz), cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var templates = (await services.Templates.GetTemplatesAsync(cancellationToken)).ToList();

        // Assert
        Assert.Equal([SurveyTemplateNames.Blank, "first", "second"], templates.Select(template => template.Name));
        Assert.Equal("First", templates[1].Title);
        Assert.Equal(SurveyKinds.Quiz, templates[1].Kind);
    }

    [Fact]
    public async Task BuildAsync_UnknownTemplate_ReturnsNull()
    {
        // Arrange
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);

        // Act
        var definition = await services.Templates.BuildAsync("nope", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(definition);
    }

    [Fact]
    public async Task CreateSurveyAsync_FromStoredTemplate_CopiesItsQuestionsWithNewIds()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var services = _harness.CreateServices(session);
        var template = Template("feedback", "Feedback", position: 10);
        var page = new SurveyPage { Id = "page-1", Name = "page_1" };

        page.Questions.Add(new SurveyQuestion { Id = "question-1", Name = "q1", ControlType = AuthoringSurveyHarness.TextControl, Title = "How was it?" });
        template.Definition.Pages.Add(page);

        await services.TemplateStore.CreateAsync(template, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        // Act
        var first = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "One", Template = "feedback" }, cancellationToken);
        var second = await services.Manager.CreateSurveyAsync(new SurveyCreateRequest { Title = "Two", Template = "feedback" }, cancellationToken);

        // Assert
        var firstQuestion = Assert.Single(first.Draft.Pages.SelectMany(item => item.Questions));
        var secondQuestion = Assert.Single(second.Draft.Pages.SelectMany(item => item.Questions));
        Assert.Equal("How was it?", firstQuestion.Title);
        Assert.NotEqual("question-1", firstQuestion.Id);
        Assert.NotEqual(firstQuestion.Id, secondQuestion.Id);
        Assert.Equal("question-1", template.Definition.Pages[0].Questions[0].Id);
    }

    [Theory]
    [InlineData("src/Modules/CrestApps.OrchardCore.Surveys/Migrations/survey-templates.recipe.json", new[] { "customer-satisfaction", "nps", "employee-engagement", "event-feedback", "quiz", "poll" })]
    [InlineData("src/Modules/CrestApps.OrchardCore.Learning.Surveys/Migrations/course-feedback-template.recipe.json", new[] { "course-feedback" })]
    public void SeedRecipes_DefineTemplatesWithQuestionsAndValidIds(string path, string[] names)
    {
        // Arrange
        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), path)))!.AsObject();

        // Act
        var step = Assert.Single(recipe["steps"]!.AsArray());
        var templates = step!["Templates"]!.AsArray()
            .Select(node => JsonSerializer.Deserialize<SurveyTemplate>(node!.ToJsonString(), CatalogDeploymentSerializer.Options))
            .ToList();

        // Assert
        Assert.Equal("SurveyTemplates", step["name"]!.GetValue<string>());
        Assert.Equal(names, templates.Select(template => template.Name));

        foreach (var template in templates)
        {
            Assert.True(CrestApps.Core.UniqueId.IsValid(template.ItemId), template.Name);
            Assert.False(string.IsNullOrWhiteSpace(template.Title));
            Assert.NotEmpty(template.Definition.Pages.SelectMany(page => page.Questions));
            Assert.Single(template.Definition.Endings, ending => ending.IsDefault);
        }

        Assert.DoesNotContain(templates, template => template.Name == SurveyTemplateNames.Blank);
    }

    private static SurveyTemplate Template(string name, string title, int position, string kind = SurveyKinds.Survey)
        => new()
        {
            ItemId = IdGenerator.GenerateId(),
            Name = name,
            Title = title,
            Kind = kind,
            Position = position,
            Definition = new SurveyDefinition { Title = title },
        };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Modules")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}
