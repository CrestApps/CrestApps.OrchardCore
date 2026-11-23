using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using OrchardCore.Deployment;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Writes the SurveyInvitations recipe step, 500 invitations at a time. Invitations hold e-mail addresses and names, so
/// only a user who may export responses gets any, as with the SurveyResponses step.
/// </summary>
internal sealed class SurveyInvitationsDeploymentSource : DeploymentSourceBase<SurveyInvitationsDeploymentStep>
{
    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;
    private readonly SurveysRecipeExporter _exporter;
    private readonly SurveysExportAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationsDeploymentSource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="exporter">The exporter.</param>
    /// <param name="authorizer">The export authorizer.</param>
    public SurveyInvitationsDeploymentSource(
        ISession session,
        SurveyStore surveyStore,
        SurveysRecipeExporter exporter,
        SurveysExportAuthorizer authorizer)
    {
        _session = session;
        _surveyStore = surveyStore;
        _exporter = exporter;
        _authorizer = authorizer;
    }

    /// <inheritdoc />
    protected override async Task ProcessAsync(SurveyInvitationsDeploymentStep step, DeploymentPlanResult result)
    {
        var invitations = new JsonArray();

        if (await _authorizer.CanExportResponsesAsync())
        {
            foreach (var survey in await SurveySelection.ListAsync(_surveyStore, step.IncludeAll, step.SurveyNames))
            {
                var surveyId = survey.ItemId;

                for (var skip = 0; ; skip += SurveysConstants.PageSize)
                {
                    var page = (await _session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
                        .OrderBy(index => index.Id)
                        .Skip(skip)
                        .Take(SurveysConstants.PageSize)
                        .ListAsync()).ToList();

                    foreach (var node in await _exporter.ExportInvitationsAsync(page))
                    {
                        invitations.Add(node.DeepClone());
                    }

                    if (page.Count < SurveysConstants.PageSize)
                    {
                        break;
                    }
                }
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Invitations,
            [SurveysRecipeSteps.InvitationsSection] = invitations,
        });
    }
}
