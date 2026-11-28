using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One row per question of each version. Answer keys are left out.
/// </summary>
internal sealed class SurveyQuestionsDataSet : SurveysDataSetBase<SurveyVersion, SurveyVersionIndex, SurveyQuestionRow>
{
    public SurveyQuestionsDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        IStringLocalizer<SurveyQuestionsDataSet> S)
        : base(new DataSetDescriptor("SurveyQuestions", S["Survey questions"], S["One row per question of each published version."]), session, authorizationService, access, SurveysConstants.CollectionName)
    {
        var group = S["Question"].Value;

        AddField("SurveyId", S["Survey ID"], DataFieldType.Text, row => row.Version.SurveyId, group).IsIdentifier = true;
        AddField("VersionNumber", S["Version"], DataFieldType.Integer, row => (long)row.Version.Number, group);
        AddField("QuestionId", S["Question ID"], DataFieldType.Text, row => row.Question.Id, group).IsIdentifier = true;
        AddField("Name", S["Name"], DataFieldType.Text, row => row.Question.Name, group);
        AddField("ControlType", S["Question type"], DataFieldType.Text, row => row.Question.ControlType, group);
        AddField("Title", S["Title"], DataFieldType.Text, row => row.Question.Title, group);
        AddField("Page", S["Page"], DataFieldType.Text, row => row.Page.Name ?? row.Page.Title, group);
        AddField("Position", S["Position"], DataFieldType.Integer, row => (long)row.Position, group);
        AddField("Points", S["Points"], DataFieldType.Decimal, row => row.Question.Scoring?.Points, group);
        AddField("Category", S["Score category"], DataFieldType.Text, row => row.Question.Scoring?.CategoryId, group);
        CompleteDescription();
    }

    protected override IEnumerable<SurveyQuestionRow> GetRows(SurveyVersion document)
    {
        var position = 0;

        foreach (var (page, question) in SurveyDefinitionQuestions.All(document.Definition))
        {
            yield return new SurveyQuestionRow(document, page, question, ++position);
        }
    }

    protected override IQueryIndex<SurveyVersionIndex> OfSurveys(IQueryIndex<SurveyVersionIndex> rows, string[] surveyIds)
        => surveyIds is null ? rows : rows.Where(index => index.SurveyId.IsIn(surveyIds));

    protected override string SurveyIdOf(SurveyQuestionRow row)
        => row?.Version?.SurveyId;
}
