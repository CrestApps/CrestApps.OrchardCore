using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Integrations;

public sealed class SurveysSampleRecipeTests
{
    [Fact]
    public async Task TheSampleRecipe_MatchesTheStepSchemas()
    {
        // Arrange
        var recipe = LoadRecipe();
        await using var harness = await IntegrationsTestHarness.CreateAsync(services => services.AddScoped<SurveyDefinitionSchemaBuilder>());

        // Act
        var results = await harness.RunAsync(async services =>
        {
            IRecipeStep[] schemas =
            [
                new SurveysRecipeStepSchema(services.GetRequiredService<SurveyDefinitionSchemaBuilder>()),
                new SurveyQuestionBanksRecipeStepSchema(services.GetRequiredService<SurveyDefinitionSchemaBuilder>()),
            ];

            var evaluations = new List<(string Step, bool Valid, string Errors)>();

            foreach (var schema in schemas)
            {
                var step = recipe["steps"]!.AsArray().OfType<JsonObject>().Single(item => item["name"]!.GetValue<string>() == schema.Name);
                var result = (await schema.GetSchemaAsync()).Evaluate(System.Text.Json.JsonSerializer.SerializeToElement(step), new EvaluationOptions { OutputFormat = OutputFormat.List });

                evaluations.Add((schema.Name, result.IsValid, string.Join("; ", (result.Details ?? []).Where(detail => detail.Errors is not null).SelectMany(detail => detail.Errors.Select(error => detail.InstanceLocation + ": " + error.Value)))));
            }

            return evaluations;
        });

        // Assert
        foreach (var (step, valid, errors) in results)
        {
            Assert.True(valid, step + " does not match its schema: " + errors);
        }
    }

    [Fact]
    public async Task TheSampleRecipe_ImportsThreePublishedSurveysWithLinks_AndABank()
    {
        // Arrange
        var recipe = LoadRecipe();
        var steps = recipe["steps"]!.AsArray().OfType<JsonObject>().ToArray();
        await using var harness = await IntegrationsTestHarness.CreateAsync();

        // Act
        var bankErrors = await harness.ImportAsync<SurveyQuestionBanksRecipeStep>(steps.Single(step => step["name"]!.GetValue<string>() == SurveysRecipeSteps.QuestionBanks));
        var surveyErrors = await harness.ImportAsync<SurveysRecipeStep>(steps.Single(step => step["name"]!.GetValue<string>() == SurveysRecipeSteps.Surveys));

        // Assert
        Assert.Empty(bankErrors);
        Assert.Empty(surveyErrors);
        var (surveys, banks) = await harness.RunAsync(async services => (
            await services.GetRequiredService<SurveyStore>().GetAllAsync(),
            await services.GetRequiredService<QuestionBankStore>().GetAllAsync()));
        Assert.Equal(3, surveys.Count);
        Assert.Single(banks);
        Assert.All(surveys, survey => Assert.False(string.IsNullOrEmpty(survey.PublishedVersionId)));
        Assert.Contains(surveys, survey => survey.Kind == "Quiz" && survey.Draft.Scoring.Enabled && survey.Draft.Scoring.PassMarkPercent == 60);
        Assert.Contains(surveys, survey => survey.Access.Anonymity == AnonymityMode.Anonymous);

        foreach (var survey in surveys)
        {
            var collectors = await harness.RunAsync(async services => await services.GetRequiredService<SurveyCollectorStore>().ListBySurveyAsync(survey.ItemId));
            Assert.Equal(22, Assert.Single(collectors).Token.Length);
        }
    }

    [Fact]
    public void TheSampleRecipe_UsesGeneratedIds_NotReadableNames()
    {
        // Arrange
        var recipe = LoadRecipe();

        // Act
        var ids = recipe.ToJsonString()
            .Split("\"ItemId\":\"", StringSplitOptions.None)
            .Skip(1)
            .Select(part => part.Substring(0, part.IndexOf('"', StringComparison.Ordinal)))
            .ToArray();

        // Assert
        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.True(CrestApps.Core.UniqueId.IsValid(id), id + " is not a generated id."));
    }

    private static JsonObject LoadRecipe()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var path = Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Surveys", "Recipes", "surveys-samples.recipe.json");

        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }
}
