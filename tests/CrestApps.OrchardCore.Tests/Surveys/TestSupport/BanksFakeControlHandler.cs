using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A question type handler that does nothing but name its type and its formula type.
/// </summary>
internal sealed class BanksFakeControlHandler : ISurveyControlHandler
{
    private readonly DataFieldType _formulaType;

    public BanksFakeControlHandler(string controlType, DataFieldType formulaType = DataFieldType.Text)
    {
        ControlType = controlType;
        _formulaType = formulaType;
    }

    public string ControlType { get; }

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
        => answer is null;

    public ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => ConditionValue.Empty;

    public object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => null;

    public DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => _formulaType;

    public string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => string.Empty;

    public IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
        => [];

    public void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
    {
    }

    public IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
        => [];
}
