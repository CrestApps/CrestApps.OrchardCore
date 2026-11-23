using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Deployment;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Writes the SurveyResponses recipe step, 500 responses at a time. Only a user who may export responses gets any;
/// each export is recorded in the audit trail.
/// </summary>
internal sealed class SurveyResponsesDeploymentSource : DeploymentSourceBase<SurveyResponsesDeploymentStep>
{
    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;
    private readonly SurveysRecipeExporter _exporter;
    private readonly SurveysExportAuthorizer _authorizer;
    private readonly ISurveyAuditTrail _auditTrail;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponsesDeploymentSource"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="exporter">The exporter.</param>
    /// <param name="authorizer">The export authorizer.</param>
    /// <param name="auditTrail">The audit trail.</param>
    public SurveyResponsesDeploymentSource(
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
    protected override async Task ProcessAsync(SurveyResponsesDeploymentStep step, DeploymentPlanResult result)
    {
        var responses = new JsonArray();

        if (await _authorizer.CanExportResponsesAsync())
        {
            await ReadAsync(step, responses);

            await _auditTrail.RecordAsync(SurveyAuditEvents.ResponsesExported, new SurveyAuditEntry
            {
                ActorId = _authorizer.CurrentUserId(),
                Detail = responses.Count.ToString(CultureInfo.InvariantCulture),
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Responses,
            [SurveysRecipeSteps.ResponsesSection] = responses,
        });
    }

    private async Task ReadAsync(SurveyResponsesDeploymentStep step, JsonArray responses)
    {
        string[] surveyIds = null;

        if (!step.IncludeAll)
        {
            var ids = new List<string>();

            foreach (var name in step.SurveyNames ?? [])
            {
                if (await _surveyStore.FindByNameAsync(name) is { } survey)
                {
                    ids.Add(survey.ItemId);
                }
            }

            if (ids.Count == 0)
            {
                return;
            }

            surveyIds = [.. ids];
        }

        var from = step.FromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = step.ToDate?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var inProgress = nameof(SurveyResponseStatus.InProgress);

        // The surveys are named a chunk at a time, so many chosen surveys never pass SQL Server's 2,100 parameters.
        IEnumerable<string[]> chunks = surveyIds is null ? [null] : SurveyIdChunks.Of(surveyIds);

        foreach (var chunk in chunks)
        {
            var skip = 0;

            while (true)
            {
                var query = _session.Query<SurveyResponse, SurveyResponseIndex>(collection: SurveysConstants.ResponsesCollectionName);

                if (chunk is not null)
                {
                    query = query.Where(index => index.SurveyId.IsIn(chunk));
                }

                if (!step.IncludeTest)
                {
                    query = query.Where(index => !index.IsTest);
                }

                if (!step.IncludeInProgress)
                {
                    query = query.Where(index => index.Status != inProgress);
                }

                if (from is { } start)
                {
                    query = query.Where(index => index.StartedUtc >= start);
                }

                if (to is { } end)
                {
                    query = query.Where(index => index.StartedUtc < end);
                }

                var page = (await query.OrderBy(index => index.Id).Skip(skip).Take(SurveysConstants.PageSize).ListAsync()).ToList();

                foreach (var node in await _exporter.ExportResponsesAsync(page))
                {
                    responses.Add(node.DeepClone());
                }

                if (page.Count < SurveysConstants.PageSize)
                {
                    break;
                }

                skip += page.Count;
            }
        }
    }
}
