using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using OrchardCore.Deployment;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Writes the Surveys recipe step. Secrets are written only when the step asks and the user may export responses.
/// </summary>
internal sealed class SurveysDeploymentSource : DeploymentSourceBase<SurveysDeploymentStep>
{
    private readonly ISession _session;
    private readonly SurveysRecipeExporter _exporter;
    private readonly SurveysExportAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysDeploymentSource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="exporter">The exporter.</param>
    /// <param name="authorizer">The export authorizer.</param>
    public SurveysDeploymentSource(
        ISession session,
        SurveysRecipeExporter exporter,
        SurveysExportAuthorizer authorizer)
    {
        _session = session;
        _exporter = exporter;
        _authorizer = authorizer;
    }

    /// <inheritdoc />
    protected override async Task ProcessAsync(SurveysDeploymentStep step, DeploymentPlanResult result)
    {
        var includeSecrets = step.IncludeSecrets && await _authorizer.CanExportResponsesAsync();
        var names = step.IncludeAll ? null : (step.SurveyNames ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var surveys = new JsonArray();
        var skip = 0;

        while (true)
        {
            var page = (await _session.Query<Survey, SurveyIndex>(collection: SurveysConstants.CollectionName)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(SurveysConstants.PageSize)
                .ListAsync()).ToList();

            foreach (var survey in page.Where(survey => names is null || names.Contains(survey.Name)))
            {
                surveys.Add(await _exporter.ExportSurveyAsync(survey, includeSecrets));
            }

            if (page.Count < SurveysConstants.PageSize)
            {
                break;
            }

            skip += page.Count;
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Surveys,
            [SurveysRecipeSteps.SurveysSection] = surveys,
        });
    }
}
