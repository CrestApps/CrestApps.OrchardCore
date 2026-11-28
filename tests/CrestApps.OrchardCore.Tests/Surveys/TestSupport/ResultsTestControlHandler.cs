using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A question type for results tests: its answer values are choice ids, its number a number, its other text a text.
/// </summary>
internal sealed class ResultsTestControlHandler : ISurveyControlHandler
{
    public const string Name = "ResultsTest";

    public string ControlType => Name;

    public void Initialize(SurveyQuestion question)
    {
    }

    public Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
        => Task.CompletedTask;

    public void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context)
    {
    }

    public JsonObject GetRespondentSettings(SurveyQuestion question)
        => [];

    public Task ReadAnswerAsync(SurveyAnswerReadContext context)
        => Task.CompletedTask;

    public Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
        => Task.CompletedTask;

    public bool IsEmpty(SurveyQuestion question, SurveyAnswer answer)
        => answer is null || (answer.Values.Count == 0 && answer.Number is null && string.IsNullOrEmpty(answer.OtherText));

    public ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => new(answer?.Values ?? []);

    public object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => answer?.Number;

    public DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => question.Choices.Count > 0 ? DataFieldType.Text : DataFieldType.Decimal;

    public string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer is null
            ? string.Empty
            : answer.Number?.ToString(CultureInfo.InvariantCulture)
                ?? string.Join(", ", answer.Values.Select(value => question.Choices.FirstOrDefault(choice => choice.Id == value)?.Text ?? value));

    public IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
    {
        yield return new SurveyExportColumn
        {
            Key = question.Name,
            Title = question.Title,
            QuestionId = question.Id,
            Type = GetFormulaType(question, null),
        };
    }

    public void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
        => row[question.Name] = answer?.Number is { } number ? number : FormatAnswer(question, answer, context);

    public IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        foreach (var value in answer.Values)
        {
            yield return new SurveyAnswerIndexRow { ChoiceId = value };
        }

        if (answer.Number is { } number)
        {
            yield return new SurveyAnswerIndexRow { NumberValue = number };
        }

        if (!string.IsNullOrEmpty(answer.OtherText))
        {
            yield return new SurveyAnswerIndexRow { TextValue = answer.OtherText };
        }
    }
}
