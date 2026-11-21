using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One row per score category of each completed response.
/// </summary>
internal sealed class SurveyCategoryScoresDataSet : SurveyResponseChildDataSet
{
    public SurveyCategoryScoresDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        ISurveyVersionCache versions,
        IStringLocalizer<SurveyCategoryScoresDataSet> S)
        : base(new DataSetDescriptor("SurveyCategoryScores", S["Survey category scores"], S["One row per score category of each completed response."]), session, authorizationService, access, versions)
    {
        var group = S["Category score"].Value;

        AddValue("ResponseId", S["Response ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("SurveyId", S["Survey ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("Category", S["Category"], DataFieldType.Text, group);
        AddValue("Score", S["Score"], DataFieldType.Decimal, group);
        CompleteDescription();
    }

    protected override IEnumerable<Dictionary<string, object>> Expand(SurveyResponse response, SurveyDefinition definition)
    {
        var names = (definition?.ScoreCategories ?? [])
            .Where(category => !string.IsNullOrEmpty(category?.Id))
            .GroupBy(category => category.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name ?? group.First().Title, StringComparer.Ordinal);

        foreach (var (category, score) in response.CategoryScores ?? [])
        {
            yield return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ResponseId"] = response.ItemId,
                ["SurveyId"] = response.SurveyId,
                ["Category"] = names.GetValueOrDefault(category) ?? category,
                ["Score"] = score,
            };
        }
    }
}
