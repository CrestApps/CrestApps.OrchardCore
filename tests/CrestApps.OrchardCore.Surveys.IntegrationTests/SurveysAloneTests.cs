using System.Net;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Surveys turned on alone migrates, opens its designer and keeps the tenant's recipe import working, with no other
/// feature that registers the shared user search service.
/// </summary>
public sealed class SurveysAloneTests : IClassFixture<SurveysAloneSiteFixture>
{
    private static readonly string[] _otherFeaturePrefixes =
    [
        "CrestApps.OrchardCore.Learning",
        "CrestApps.OrchardCore.Reports.Builder",
        "CrestApps.OrchardCore.Users",
        "CrestApps.OrchardCore.Calendars",
        "CrestApps.OrchardCore.Assets",
    ];

    private readonly SurveysAloneSiteFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysAloneTests"/> class.
    /// </summary>
    /// <param name="fixture">The site.</param>
    public SurveysAloneTests(SurveysAloneSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Feature_EnabledAlone_RunsItsMigrationsAndResolvesEveryRecipeStep()
    {
        // Act
        var (enabled, others, handlers, templatesSeeded) = await _fixture.InTenantAsync(async services =>
        {
            var features = (await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToList();
            var stepHandlers = services.GetServices<IRecipeStepHandler>().Count();
            var templates = await services.GetRequiredService<SurveyTemplateStore>().ListOrderedAsync(TestContext.Current.CancellationToken);

            return (
                features.Contains(SurveysConstants.Features.Surveys),
                features.Where(id => _otherFeaturePrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))).ToList(),
                stepHandlers,
                templates.Count > 0);
        });

        // Assert
        Assert.True(enabled);
        Assert.Empty(others);
        Assert.True(handlers > 0);
        Assert.True(templatesSeeded, "The migration recipe that seeds the survey templates did not run.");
    }

    [Fact]
    public async Task NewSurveyPage_EnabledAlone_Opens()
    {
        // Arrange
        using var client = await _fixture.CreateClientAsync("admin");

        // Act
        var response = await client.GetAsync("Admin/surveys/create", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RecipeImport_EnabledAlone_ImportsASurveyWithAPersonByUserName()
    {
        // Arrange
        var recipe = new JsonObject
        {
            ["name"] = "SurveysAloneImport",
            ["steps"] = new JsonArray(new JsonObject
            {
                ["name"] = "Surveys",
                ["Surveys"] = new JsonArray(new JsonObject
                {
                    ["Name"] = "alone-import",
                    ["Title"] = "Imported alone",
                    ["PublishedVersionNumber"] = 1,
                    ["Versions"] = new JsonArray(new JsonObject
                    {
                        ["Number"] = 1,
                        ["PublishedByUserName"] = "admin",
                    }),
                }),
            }),
        };

        // Act
        await _fixture.InTenantAsync(services => SurveysSiteFixture.RunRecipeAsync(services, recipe));
        var (title, publishedBy) = await _fixture.InTenantAsync(async services =>
        {
            var survey = await services.GetRequiredService<SurveyStore>().FindByNameAsync("alone-import");
            var versions = await services.GetRequiredService<SurveyVersionStore>().ListBySurveyAsync(survey.ItemId, TestContext.Current.CancellationToken);

            return (survey.Title, versions.Single().PublishedBy);
        });

        // Assert
        Assert.Equal("Imported alone", title);
        Assert.False(string.IsNullOrEmpty(publishedBy));
    }
}
