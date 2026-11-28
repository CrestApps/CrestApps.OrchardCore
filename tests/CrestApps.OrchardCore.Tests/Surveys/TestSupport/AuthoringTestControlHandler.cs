using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A minimal question type handler for authoring tests: it collects text and adds two choices to new choice questions.
/// </summary>
internal sealed class AuthoringTestControlHandler : ISurveyControlHandler
{
    private readonly bool _choices;

    public AuthoringTestControlHandler(string controlType, bool choices = false)
    {
        ControlType = controlType;
        _choices = choices;
    }

    public string ControlType { get; }

    public void Initialize(SurveyQuestion question)
    {
        if (_choices && question.Choices.Count == 0)
        {
            question.Choices.Add(new SurveyChoice { Id = "c1-" + Guid.NewGuid().ToString("N"), Value = "1", Text = "One" });
            question.Choices.Add(new SurveyChoice { Id = "c2-" + Guid.NewGuid().ToString("N"), Value = "2", Text = "Two" });
        }
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
        => answer is null || answer.Values.Count == 0;

    public ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => new(answer?.Values ?? []);

    public object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => answer?.Values.FirstOrDefault();

    public DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Text;

    public string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => string.Join(", ", answer?.Values ?? []);

    public IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
        => [new SurveyExportColumn { Key = question.Name, QuestionId = question.Id }];

    public void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
        => row[question.Name] = FormatAnswer(question, answer, context);

    public IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
        => [];
}
