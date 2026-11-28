using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One row per survey.
/// </summary>
internal sealed class SurveysDataSet : SurveysDataSetBase<Survey, SurveyIndex, Survey>
{
    public SurveysDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        IStringLocalizer<SurveysDataSet> S)
        : base(new DataSetDescriptor("Surveys", S["Surveys"], S["One row per survey."]), session, authorizationService, access, SurveysConstants.CollectionName)
    {
        var group = S["Survey"].Value;

        AddField("Id", S["Survey ID"], DataFieldType.Text, survey => survey.ItemId, group).IsIdentifier = true;
        AddField("Name", S["Name"], DataFieldType.Text, survey => survey.Name, group);
        AddField("Title", S["Title"], DataFieldType.Text, survey => survey.Title, group);
        AddField("Kind", S["Kind"], DataFieldType.Text, survey => survey.Kind, group);
        AddField("Status", S["Status"], DataFieldType.Text, survey => survey.Status.ToString(), group);
        AddField("Owner", S["Owner"], DataFieldType.Text, survey => survey.Owner?.ToString(), group);
        AddField("PublishedVersion", S["Published version"], DataFieldType.Integer, survey => (long)survey.PublishedVersionNumber, group);
        AddField("Created", S["Created"], DataFieldType.DateTime, survey => DateTime.SpecifyKind(survey.CreatedUtc, DateTimeKind.Utc), group);
        AddField("Modified", S["Modified"], DataFieldType.DateTime, survey => survey.ModifiedUtc is { } modified ? DateTime.SpecifyKind(modified, DateTimeKind.Utc) : null, group);
        CompleteDescription();
    }

    protected override IEnumerable<Survey> GetRows(Survey document)
    {
        yield return document;
    }

    protected override IQueryIndex<SurveyIndex> OfSurveys(IQueryIndex<SurveyIndex> rows, string[] surveyIds)
        => surveyIds is null ? rows : rows.Where(index => index.ItemId.IsIn(surveyIds));

    protected override string SurveyIdOf(Survey row)
        => row?.ItemId;
}
