using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using OrchardCore.Deployment;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Writes the SurveyInvitations recipe step.
/// </summary>
internal sealed class SurveyInvitationsDeploymentSource : DeploymentSourceBase<SurveyInvitationsDeploymentStep>
{
    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;
    private readonly SurveysRecipeExporter _exporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationsDeploymentSource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="exporter">The exporter.</param>
    public SurveyInvitationsDeploymentSource(
        ISession session,
        SurveyStore surveyStore,
        SurveysRecipeExporter exporter)
    {
        _session = session;
        _surveyStore = surveyStore;
        _exporter = exporter;
    }

    /// <inheritdoc />
    protected override async Task ProcessAsync(SurveyInvitationsDeploymentStep step, DeploymentPlanResult result)
    {
        var invitations = new List<SurveyInvitation>();

        foreach (var survey in await SurveySelection.ListAsync(_surveyStore, step.IncludeAll, step.SurveyNames))
        {
            var surveyId = survey.ItemId;

            invitations.AddRange(await _session.Query<SurveyInvitation, SurveyInvitationIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.CollectionName)
                .OrderBy(index => index.Id)
                .ListAsync());
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Invitations,
            [SurveysRecipeSteps.InvitationsSection] = await _exporter.ExportInvitationsAsync(invitations),
        });
    }
}
