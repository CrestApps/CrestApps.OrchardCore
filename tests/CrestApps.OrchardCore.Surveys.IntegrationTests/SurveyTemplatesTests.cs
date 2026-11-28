using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Turning Surveys on seeds the default survey templates through a migration recipe, and the SurveyTemplates recipe
/// step adds and updates templates by name.
/// </summary>
[Collection(SurveysSiteCollection.Name)]
public sealed class SurveyTemplatesTests
{
    private readonly SurveysSiteFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplatesTests"/> class.
    /// </summary>
    /// <param name="fixture">The site.</param>
    public SurveyTemplatesTests(SurveysSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Feature_WhenEnabled_SeedsTheDefaultTemplates()
    {
        // Act
        var names = await _fixture.InTenantAsync(async services =>
        {
            var providers = services.GetServices<ISurveyTemplateProvider>();
            var result = new List<string>();

            foreach (var provider in providers)
            {
                result.AddRange((await provider.GetTemplatesAsync()).Select(template => template.Name));
            }

            return result;
        });

        // Assert
        Assert.Contains(SurveyTemplateNames.Blank, names);
        Assert.Contains(SurveyTemplateNames.CustomerSatisfaction, names);
        Assert.Contains(SurveyTemplateNames.NetPromoterScore, names);
        Assert.Contains(SurveyTemplateNames.Quiz, names);
        Assert.Contains(SurveyTemplateNames.Poll, names);
    }

    [Fact]
    public async Task RecipeStep_TemplateOfAnExistingName_UpdatesIt()
    {
        // Arrange
        var recipe = new JsonObject
        {
            ["name"] = "SurveyTemplatesUpdate",
            ["steps"] = new JsonArray(new JsonObject
            {
                ["name"] = "SurveyTemplates",
                ["Templates"] = new JsonArray(new JsonObject
                {
                    ["Name"] = SurveyTemplateNames.Poll,
                    ["Title"] = "Quick poll",
                    ["Kind"] = SurveyKinds.Poll,
                    ["Position"] = 5,
                }),
            }),
        };

        // Act
        await _fixture.InTenantAsync(services => SurveysSiteFixture.RunRecipeAsync(services, recipe));
        var (title, count) = await _fixture.InTenantAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyTemplateStore>();
            var templates = await store.ListOrderedAsync();

            return (templates.Single(template => template.Name == SurveyTemplateNames.Poll).Title, templates.Count(template => template.Name == SurveyTemplateNames.Poll));
        });

        // Assert
        Assert.Equal("Quick poll", title);
        Assert.Equal(1, count);
    }
}
