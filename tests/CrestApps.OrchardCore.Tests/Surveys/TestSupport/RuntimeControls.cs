using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Simple control handlers for runtime tests: they read <c>q.{id}</c> as text or as choice ids.
/// </summary>
internal abstract class RuntimeControlBase : ISurveyControlHandler
{
    public abstract string ControlType { get; }

    public void Initialize(SurveyQuestion question)
    {
    }

    public Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
        => Task.CompletedTask;

    public virtual void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context)
    {
    }

    public JsonObject GetRespondentSettings(SurveyQuestion question)
        => [];

    public virtual Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        var values = context.Form is not null
            ? context.Form[context.Prefix].Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList()
            : context.Json is JsonValue json ? [json.GetValue<string>()] : [];

        context.Answer = values.Count == 0 ? null : new SurveyAnswer { Values = Filter(values, context) };

        return Task.CompletedTask;
    }

    public virtual Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
        => Task.CompletedTask;

    public bool IsEmpty(SurveyQuestion question, SurveyAnswer answer)
        => answer is null || answer.Values.Count == 0;

    public ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => new(answer?.Values ?? []);

    public virtual object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => answer is null ? null : string.Join(",", answer.Values);

    public virtual DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Text;

    public string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer is null
            ? string.Empty
            : string.Join(", ", answer.Values.Select(value => question.Choices.FirstOrDefault(choice => choice.Id == value)?.Text ?? value));

    public IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
        => [new SurveyExportColumn { Key = question.Name, QuestionId = question.Id }];

    public void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
        => row[question.Name] = FormatAnswer(question, answer, context);

    public IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
        => [];

    protected virtual List<string> Filter(List<string> values, SurveyAnswerReadContext context)
        => values;
}

/// <summary>
/// A text question for runtime tests.
/// </summary>
internal sealed class RuntimeTextControl : RuntimeControlBase
{
    public const string Name = "RuntimeText";

    public override string ControlType => Name;
}

/// <summary>
/// A number question for runtime tests.
/// </summary>
internal sealed class RuntimeNumberControl : RuntimeControlBase
{
    public const string Name = "RuntimeNumber";

    public override string ControlType => Name;

    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => answer?.Values.Count > 0 && decimal.TryParse(answer.Values[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;

    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Decimal;

    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        if (context.Answer is not null && GetFormulaValue(context.Question, context.Answer, null) is null)
        {
            context.Errors.Add("Enter a number.");
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// A choice question for runtime tests: values are choice ids, limited to the choices the respondent may pick.
/// </summary>
internal sealed class RuntimeChoiceControl : RuntimeControlBase
{
    public const string Name = "RuntimeChoice";

    public override string ControlType => Name;

    protected override List<string> Filter(List<string> values, SurveyAnswerReadContext context)
        => values.Where(context.AllowedChoiceIds.Contains).ToList();
}

/// <summary>
/// A text block that collects no answer.
/// </summary>
internal sealed class RuntimeBlockControl : RuntimeControlBase
{
    public const string Name = "RuntimeBlock";

    public override string ControlType => Name;
}
