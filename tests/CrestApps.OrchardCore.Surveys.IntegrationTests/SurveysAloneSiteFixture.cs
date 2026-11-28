namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// A site with Surveys and nothing it integrates with: no Learning, no Report Builder and no CrestApps Users feature,
/// as on most real sites.
/// </summary>
public sealed class SurveysAloneSiteFixture : SurveysSiteFixture
{
    private static readonly string[] _features =
    [
        "OrchardCore.Recipes",
        "OrchardCore.Deployment",
        SurveysConstants.Features.Surveys,
    ];

    /// <inheritdoc />
    protected override IReadOnlyCollection<string> Features => _features;

    /// <inheritdoc />
    protected override bool ConfiguresRespondentSite => false;
}
