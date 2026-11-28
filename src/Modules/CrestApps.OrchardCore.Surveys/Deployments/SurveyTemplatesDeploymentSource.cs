using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Writes the survey templates of a deployment plan.
/// </summary>
internal sealed class SurveyTemplatesDeploymentSource : DeploymentSourceBase<SurveyTemplatesDeploymentStep>
{
    private readonly SurveyTemplateStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplatesDeploymentSource"/> class.
    /// </summary>
    /// <param name="store">The template store.</param>
    public SurveyTemplatesDeploymentSource(SurveyTemplateStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    protected override async Task ProcessAsync(SurveyTemplatesDeploymentStep step, DeploymentPlanResult result)
    {
        var names = new HashSet<string>(step.TemplateNames ?? [], StringComparer.OrdinalIgnoreCase);
        var entries = new JsonArray();

        foreach (var template in await _store.ListOrderedAsync())
        {
            if (!step.IncludeAll && !names.Contains(template.Name))
            {
                continue;
            }

            entries.Add(SurveysRecipeJson.Write(template));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Templates,
            [SurveysRecipeSteps.TemplatesSection] = entries,
        });
    }
}
