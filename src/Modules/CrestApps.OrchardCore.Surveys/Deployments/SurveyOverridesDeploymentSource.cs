using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Writes the SurveyOverrides recipe step.
/// </summary>
internal sealed class SurveyOverridesDeploymentSource : DeploymentSourceBase<SurveyOverridesDeploymentStep>
{
    private readonly SurveyStore _surveyStore;
    private readonly SurveyOverrideStore _overrideStore;
    private readonly SurveysRecipeExporter _exporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyOverridesDeploymentSource"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="overrideStore">The accommodation store.</param>
    /// <param name="exporter">The exporter.</param>
    public SurveyOverridesDeploymentSource(
        SurveyStore surveyStore,
        SurveyOverrideStore overrideStore,
        SurveysRecipeExporter exporter)
    {
        _surveyStore = surveyStore;
        _overrideStore = overrideStore;
        _exporter = exporter;
    }

    /// <inheritdoc />
    protected override async Task ProcessAsync(SurveyOverridesDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = new List<SurveyOverride>();

        foreach (var survey in await SurveySelection.ListAsync(_surveyStore, step.IncludeAll, step.SurveyNames))
        {
            entries.AddRange(await _overrideStore.ListBySurveyAsync(survey.ItemId));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Overrides,
            [SurveysRecipeSteps.OverridesSection] = await _exporter.ExportOverridesAsync(entries),
        });
    }
}
