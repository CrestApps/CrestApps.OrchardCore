using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;

/// <summary>
/// Several choices from a list.
/// </summary>
public sealed class MultipleChoiceControlHandler : SurveyChoiceControlHandlerBase<MultipleChoiceSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipleChoiceControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MultipleChoiceControlHandler(IStringLocalizer<MultipleChoiceControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.MultipleChoice;

    /// <inheritdoc />
    protected override bool AllowsMultiple => true;

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        var settings = question.GetSettings<MultipleChoiceSettings>();

        if (settings.MinSelected is { } min && settings.MaxSelected is { } max && min > max)
        {
            context.AddError("min-max", S["The fewest choices of question {0} is more than the most choices.", question.Name], question.Id);
        }
    }

    /// <inheritdoc />
    public override async Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        await base.ValidateAnswerAsync(context);

        if (IsEmpty(context.Question, context.Answer))
        {
            return;
        }

        var settings = context.Question.GetSettings<MultipleChoiceSettings>();
        var count = context.Answer.Values.Count;
        var exclusive = context.Question.Choices.Any(choice => choice.IsExclusive && context.Answer.Values.Contains(choice.Id, StringComparer.Ordinal));

        if (!exclusive && settings.MinSelected is { } min && count < min)
        {
            context.Errors.Add(S["Select at least {0} options.", min]);
        }

        if (settings.MaxSelected is { } max && count > max)
        {
            context.Errors.Add(S["Select at most {0} options.", max]);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        yield return new SurveyExportColumn { Key = question.Name, Title = question.Title, QuestionId = question.Id };

        foreach (var choice in question.Choices.Where(choice => choice is not null))
        {
            yield return new SurveyExportColumn
            {
                Key = $"{question.Name}.{ColumnName(choice)}",
                Title = $"{question.Title}: {choice.Text}",
                QuestionId = question.Id,
                RowId = choice.Id,
                Type = DataFieldType.Boolean,
            };
        }
    }

    /// <inheritdoc />
    public override void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(row);

        row[question.Name] = FormatAnswer(question, answer, context);

        foreach (var choice in question.Choices.Where(choice => choice is not null))
        {
            row[$"{question.Name}.{ColumnName(choice)}"] = answer?.Values?.Contains(choice.Id, StringComparer.Ordinal) == true;
        }
    }

    private static string ColumnName(SurveyChoice choice)
        => string.IsNullOrWhiteSpace(choice.Value) ? choice.Id : choice.Value;
}
