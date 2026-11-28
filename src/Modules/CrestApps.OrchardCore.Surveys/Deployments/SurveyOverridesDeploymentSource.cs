using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Deployment;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Writes the SurveyOverrides recipe step, 500 accommodations at a time. Accommodations name people and say why they
/// need extra time, so only a user who may export responses gets any, as with the SurveyResponses step.
/// </summary>
internal sealed class SurveyOverridesDeploymentSource : DeploymentSourceBase<SurveyOverridesDeploymentStep>
{
    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;
    private readonly SurveysRecipeExporter _exporter;
    private readonly SurveysExportAuthorizer _authorizer;
    private readonly ISurveyAuditTrail _auditTrail;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyOverridesDeploymentSource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="exporter">The exporter.</param>
    /// <param name="authorizer">The export authorizer.</param>
    /// <param name="auditTrail">The audit trail, where each export (or refusal) is recorded.</param>
    public SurveyOverridesDeploymentSource(
        ISession session,
        SurveyStore surveyStore,
        SurveysRecipeExporter exporter,
        SurveysExportAuthorizer authorizer,
        ISurveyAuditTrail auditTrail)
    {
        _session = session;
        _surveyStore = surveyStore;
        _exporter = exporter;
        _authorizer = authorizer;
        _auditTrail = auditTrail;
    }

    /// <inheritdoc />
    protected override async Task ProcessAsync(SurveyOverridesDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = new JsonArray();

        var recipeStep = new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Overrides,
            [SurveysRecipeSteps.OverridesSection] = entries,
        };

        if (await _authorizer.CanExportResponsesAsync())
        {
            foreach (var survey in await SurveySelection.ListAsync(_surveyStore, step.IncludeAll, step.SurveyNames))
            {
                var surveyId = survey.ItemId;

                for (var skip = 0; ; skip += SurveysConstants.PageSize)
                {
                    var page = (await _session.Query<SurveyOverride, SurveyOverrideIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
                        .OrderBy(index => index.Id)
                        .Skip(skip)
                        .Take(SurveysConstants.PageSize)
                        .ListAsync()).ToList();

                    foreach (var node in await _exporter.ExportOverridesAsync(page))
                    {
                        entries.Add(node.DeepClone());
                    }

                    if (page.Count < SurveysConstants.PageSize)
                    {
                        break;
                    }
                }
            }

            await _authorizer.ExportedAsync(_auditTrail, SurveyAuditEvents.OverridesExported, entries.Count);
        }
        else
        {
            await _authorizer.RefusedAsync(_auditTrail, recipeStep, SurveysRecipeSteps.Overrides);
        }

        result.Steps.Add(recipeStep);
    }
}
