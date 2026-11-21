using System.Linq.Expressions;
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
/// One row per published version.
/// </summary>
internal sealed class SurveyVersionsDataSet : SurveysDataSetBase<SurveyVersion, SurveyVersionIndex, SurveyVersion>
{
    public SurveyVersionsDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        IStringLocalizer<SurveyVersionsDataSet> S)
        : base(new DataSetDescriptor("SurveyVersions", S["Survey versions"], S["One row per published version of a survey."]), session, authorizationService, access, SurveysConstants.CollectionName)
    {
        var group = S["Version"].Value;

        AddField("Id", S["Version ID"], DataFieldType.Text, version => version.ItemId, group).IsIdentifier = true;
        AddField("SurveyId", S["Survey ID"], DataFieldType.Text, version => version.SurveyId, group).IsIdentifier = true;
        AddField("Number", S["Number"], DataFieldType.Integer, version => (long)version.Number, group);
        AddField("PublishedUtc", S["Published"], DataFieldType.DateTime, version => DateTime.SpecifyKind(version.PublishedUtc, DateTimeKind.Utc), group);
        AddField("PublishedBy", S["Published by"], DataFieldType.Text, version => version.PublishedBy, group).IsIdentifier = true;
        AddField("Notes", S["Notes"], DataFieldType.Text, version => version.Notes, group);
        CompleteDescription();
    }

    protected override (string Field, Expression<Func<SurveyVersionIndex, DateTime>> Column)? DateColumn
        => ("PublishedUtc", index => index.PublishedUtc);

    protected override IEnumerable<SurveyVersion> GetRows(SurveyVersion document)
    {
        yield return document;
    }

    protected override IQueryIndex<SurveyVersionIndex> OfSurveys(IQueryIndex<SurveyVersionIndex> rows, string[] surveyIds)
        => rows.Where(index => index.SurveyId.IsIn(surveyIds));
}
