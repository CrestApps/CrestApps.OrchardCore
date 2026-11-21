using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// The Surveys feature turns on and registers its services.
/// </summary>
[Collection(SurveysSiteCollection.Name)]
public sealed class SurveysFeatureTests
{
    private readonly SurveysSiteFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysFeatureTests"/> class.
    /// </summary>
    /// <param name="fixture">The site.</param>
    public SurveysFeatureTests(SurveysSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Feature_WhenEnabled_RegistersTheVersionCacheAndTheControlRegistry()
    {
        // Act
        var (enabled, hasCache, hasRegistry) = await _fixture.InTenantAsync(async services =>
        {
            var features = await services.GetRequiredService<IShellFeaturesManager>().GetEnabledFeaturesAsync();

            return (
                features.Any(feature => feature.Id == SurveysConstants.Features.Surveys),
                services.GetService<ISurveyVersionCache>() is not null,
                services.GetService<ISurveyControlRegistry>() is not null);
        });

        // Assert
        Assert.True(enabled);
        Assert.True(hasCache);
        Assert.True(hasRegistry);
    }
}
