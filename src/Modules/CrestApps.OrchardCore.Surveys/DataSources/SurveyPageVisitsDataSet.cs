using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One row per page visit of each response, finished or not, for funnels and timings.
/// </summary>
internal sealed class SurveyPageVisitsDataSet : SurveyResponseChildDataSet
{
    public SurveyPageVisitsDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        ISurveyVersionCache versions,
        IStringLocalizer<SurveyPageVisitsDataSet> S)
        : base(new DataSetDescriptor("SurveyPageVisits", S["Survey page visits"], S["One row per page visit of each response."]), session, authorizationService, access, versions)
    {
        var group = S["Page visit"].Value;

        AddValue("ResponseId", S["Response ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("SurveyId", S["Survey ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("PageName", S["Page"], DataFieldType.Text, group);
        AddValue("EnteredUtc", S["Entered"], DataFieldType.DateTime, group);
        AddValue("LeftUtc", S["Left"], DataFieldType.DateTime, group);
        AddValue("Seconds", S["Seconds"], DataFieldType.Integer, group);
        CompleteDescription();
    }

    protected override IEnumerable<Dictionary<string, object>> Expand(SurveyResponse response, SurveyDefinition definition)
    {
        var pages = (definition?.Pages ?? [])
            .Where(page => !string.IsNullOrEmpty(page?.Id))
            .GroupBy(page => page.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name ?? group.First().Title, StringComparer.Ordinal);

        foreach (var visit in response.PageVisits ?? [])
        {
            var pageId = response.Plan?.ScreenPages?.GetValueOrDefault(visit.PageId ?? string.Empty) ?? visit.PageId;

            yield return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ResponseId"] = response.ItemId,
                ["SurveyId"] = response.SurveyId,
                ["PageName"] = pages.GetValueOrDefault(pageId ?? string.Empty) ?? pageId,
                ["EnteredUtc"] = TimeOf(response.SurveyId, visit.EnteredUtc),
                ["LeftUtc"] = TimeOf(response.SurveyId, visit.LeftUtc),
                ["Seconds"] = visit.LeftUtc is { } left && left >= visit.EnteredUtc ? (long)(left - visit.EnteredUtc).TotalSeconds : null,
            };
        }
    }

    protected override IQueryIndex<SurveyResponseIndex> OfSurveys(IQueryIndex<SurveyResponseIndex> rows, string[] surveyIds)
        => surveyIds is null
            ? rows.Where(index => !index.IsTest)
            : rows.Where(index => index.SurveyId.IsIn(surveyIds) && !index.IsTest);

    protected override System.Linq.Expressions.Expression<Func<SurveyResponseIndex, bool>> AnonymousRowsOf(string surveyId)
        => index => index.SurveyId == surveyId && !index.IsTest;
}
