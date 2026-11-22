using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One row per answer value of a completed response (long form): a chosen choice, a number, a date or a text.
/// </summary>
internal sealed class SurveyAnswersDataSet : SurveyResponseChildDataSet
{
    private readonly ISurveyControlRegistry _registry;

    public SurveyAnswersDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        ISurveyVersionCache versions,
        ISurveyControlRegistry registry,
        IStringLocalizer<SurveyAnswersDataSet> S)
        : base(new DataSetDescriptor("SurveyAnswers", S["Survey answers"], S["One row per answer value of a completed response."]), session, authorizationService, access, versions)
    {
        _registry = registry;

        var group = S["Answer"].Value;

        AddValue("ResponseId", S["Response ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("SurveyId", S["Survey ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("VersionNumber", S["Version"], DataFieldType.Integer, group);
        AddValue("QuestionId", S["Question ID"], DataFieldType.Text, group).IsIdentifier = true;
        AddValue("QuestionName", S["Question"], DataFieldType.Text, group);
        AddValue("RowId", S["Row ID"], DataFieldType.Text, group);
        AddValue("ChoiceId", S["Choice ID"], DataFieldType.Text, group);
        AddValue("ChoiceValue", S["Choice value"], DataFieldType.Text, group);
        AddValue("ChoiceText", S["Choice text"], DataFieldType.Text, group);
        AddValue("NumberValue", S["Number"], DataFieldType.Decimal, group);
        AddValue("DateValue", S["Date"], DataFieldType.DateTime, group);
        AddValue("TextValue", S["Text"], DataFieldType.Text, group);
        AddValue("Score", S["Score"], DataFieldType.Decimal, group);
        AddValue("MaxPoints", S["Max points"], DataFieldType.Decimal, group);
        AddValue("CompletedUtc", S["Completed"], DataFieldType.DateTime, group);
        CompleteDescription();
    }

    protected override IEnumerable<Dictionary<string, object>> Expand(SurveyResponse response, SurveyDefinition definition)
    {
        if (definition is null)
        {
            yield break;
        }

        var questions = SurveyDefinitionQuestions.ById(definition);

        foreach (var row in SurveyAnswerIndexRows.Build(response, definition, _registry))
        {
            var choice = questions.GetValueOrDefault(row.QuestionId ?? string.Empty)?.Choices.FirstOrDefault(candidate => candidate.Id == row.ChoiceId);

            yield return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ResponseId"] = row.ResponseId,
                ["SurveyId"] = row.SurveyId,
                ["VersionNumber"] = (long)row.VersionNumber,
                ["QuestionId"] = row.QuestionId,
                ["QuestionName"] = row.QuestionName,
                ["RowId"] = row.RowId,
                ["ChoiceId"] = row.ChoiceId,
                ["ChoiceValue"] = choice?.Value,
                ["ChoiceText"] = choice?.Text,
                ["NumberValue"] = row.NumberValue,
                ["DateValue"] = Utc(row.DateValue),
                ["TextValue"] = row.TextValue,
                ["Score"] = row.Score,
                ["MaxPoints"] = row.MaxPoints,
                ["CompletedUtc"] = TimeOf(response.SurveyId, row.CompletedUtc),
            };
        }
    }
}
