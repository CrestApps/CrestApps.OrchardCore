using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Access;
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
        var recipeStep = new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Responses,
            [SurveysRecipeSteps.ResponsesSection] = responses,
        };

        if (await _authorizer.CanExportResponsesAsync())
        {
            await ReadAsync(step, responses);
            await _authorizer.ExportedAsync(_auditTrail, SurveyAuditEvents.ResponsesExported, responses.Count);
        }
        else
        {
            await _authorizer.RefusedAsync(_auditTrail, recipeStep, SurveysRecipeSteps.Responses);
        }

        result.Steps.Add(recipeStep);
    }

    private async Task ReadAsync(SurveyResponsesDeploymentStep step, JsonArray responses)
    {
        string[] surveyIds = null;
        HashSet<string> anonymous;

        if (!step.IncludeAll)
        {
            var ids = new List<string>();
            anonymous = new HashSet<string>(StringComparer.Ordinal);

            foreach (var name in step.SurveyNames ?? [])
            {
                if (await _surveyStore.FindByNameAsync(name) is { } survey)
                {
                    ids.Add(survey.ItemId);

                    if (SurveyAnonymity.IsAnonymous(survey))
                    {
                        anonymous.Add(survey.ItemId);
                    }
                }
            }

            if (ids.Count == 0)
            {
                return;
            }

            surveyIds = [.. ids];
        }
        else
        {
            // A survey whose index row doesn't hold its anonymity yet counts as anonymous, which only changes its order.
            var identified = nameof(AnonymityMode.Identified);
            anonymous = (await _session.QueryIndex<SurveyIndex>(index => index.Anonymity == null || index.Anonymity != identified, collection: SurveysConstants.CollectionName).ListAsync())
                .Select(row => row.ItemId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.Ordinal);
        }

        // Rows are written in the order they were stored, which follows when people responded, so an anonymous survey's
        // rows are written apart, after the others, in the order of their random ids: next to the rows of identified
        // surveys, an anonymous row would otherwise tell when it came in. Their times are cut to the day by the exporter.
        IEnumerable<string[]> chunks = surveyIds is null ? [null] : SurveyIdChunks.Of(surveyIds.Where(id => !anonymous.Contains(id)));

        foreach (var chunk in chunks)
        {
            await ReadPagesAsync(step, chunk, byId: false, row => !anonymous.Contains(row.SurveyId ?? string.Empty), responses);
        }

        foreach (var surveyId in anonymous.Where(id => surveyIds is null || surveyIds.Contains(id, StringComparer.Ordinal)).Order(StringComparer.Ordinal))
        {
            await ReadPagesAsync(step, [surveyId], byId: true, _ => true, responses);
        }
    }

    // Reads the responses of some surveys (every survey when none are named, at most one IN list of them otherwise) a
    // page at a time, in the order they were stored or of their ids, and writes those a filter keeps.
    private async Task ReadPagesAsync(SurveyResponsesDeploymentStep step, string[] chunk, bool byId, Func<SurveyResponse, bool> keep, JsonArray responses)
    {
        var from = step.FromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = step.ToDate?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var inProgress = nameof(SurveyResponseStatus.InProgress);
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

            var ordered = byId ? query.OrderBy(index => index.ItemId) : query.OrderBy(index => index.Id);
            var page = (await ordered.Skip(skip).Take(SurveysConstants.PageSize).ListAsync()).ToList();

            foreach (var node in await _exporter.ExportResponsesAsync([.. page.Where(keep)]))
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
