using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;

/// <summary>
/// A grid of rows and columns. Each row's answer holds column (choice) ids.
/// </summary>
public sealed class MatrixControlHandler : SurveyChoiceControlHandlerBase<MatrixSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MatrixControlHandler(IStringLocalizer<MatrixControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Matrix;

    /// <inheritdoc />
    public override void Initialize(SurveyQuestion question)
    {
        base.Initialize(question);

        if (question.Rows.Count == 0)
        {
            question.Rows.Add(new SurveySubQuestion { Id = IdGenerator.GenerateId(), Name = "row_1", Text = S["Row 1"] });
            question.Rows.Add(new SurveySubQuestion { Id = IdGenerator.GenerateId(), Name = "row_2", Text = S["Row 2"] });
        }
    }

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        if (!question.Rows.Any(row => row is not null && !row.Retired))
        {
            context.AddError("no-rows", S["Question {0} needs at least one row.", question.Name], question.Id);
        }
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Question.GetSettings<MatrixSettings>();
        var columns = context.Question.Choices.Where(choice => choice is not null && !choice.Retired).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var rows = SurveyRowAnswers.Read(context, columns, settings.Mode == MatrixMode.Multiple);

        context.Answer = rows.Count == 0 ? null : new SurveyAnswer { Rows = rows };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsRequired)
        {
            return Task.CompletedTask;
        }

        var settings = context.Question.GetSettings<MatrixSettings>();
        var answered = context.Answer?.Rows?.Count(row => row.Value?.Count > 0) ?? 0;
        var active = context.Question.Rows.Count(row => row is not null && !row.Retired);

        if (answered == 0)
        {
            context.Errors.Add(S["This question requires an answer."]);
        }
        else if (settings.AllRowsRequired && answered < active)
        {
            context.Errors.Add(S["Answer every row."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
    {
        if (answer?.Rows is null)
        {
            return ConditionValue.Empty;
        }

        if (!string.IsNullOrEmpty(rowId))
        {
            return answer.Rows.TryGetValue(rowId, out var row) ? new ConditionValue(row ?? []) : ConditionValue.Empty;
        }

        return new ConditionValue(answer.Rows.SelectMany(row => row.Value ?? []).Distinct(StringComparer.Ordinal));
    }

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
    {
        if (string.IsNullOrEmpty(rowId) || answer?.Rows is null || !answer.Rows.TryGetValue(rowId, out var values) || values is not { Count: > 0 })
        {
            return null;
        }

        return string.Join(", ", values.Select(id => FindChoice(question, id)?.Value ?? id));
    }

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        if (answer?.Rows is not { Count: > 0 })
        {
            return string.Empty;
        }

        var parts = new List<string>();

        foreach (var row in question.Rows.Where(row => row is not null))
        {
            if (answer.Rows.TryGetValue(row.Id, out var values) && values?.Count > 0)
            {
                parts.Add($"{row.Text}: {FormatValues(question, values, context)}");
            }
        }

        return string.Join("; ", parts);
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        foreach (var row in question.Rows.Where(row => row is not null))
        {
            yield return new SurveyExportColumn
            {
                Key = $"{question.Name}.{RowName(row)}",
                Title = $"{question.Title}: {row.Text}",
                QuestionId = question.Id,
                RowId = row.Id,
            };
        }
    }

    /// <inheritdoc />
    public override void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(row);

        foreach (var sub in question.Rows.Where(sub => sub is not null))
        {
            row[$"{question.Name}.{RowName(sub)}"] = answer?.Rows is not null && answer.Rows.TryGetValue(sub.Id, out var values) && values?.Count > 0
                ? FormatValues(question, values, context)
                : string.Empty;
        }
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        foreach (var (rowId, values) in answer?.Rows ?? [])
        {
            foreach (var choiceId in values ?? [])
            {
                yield return new SurveyAnswerIndexRow { RowId = rowId, ChoiceId = choiceId };
            }
        }
    }

    private static string RowName(SurveySubQuestion row)
        => string.IsNullOrWhiteSpace(row.Name) ? row.Id : row.Name;

    private static string FormatValues(SurveyQuestion question, IEnumerable<string> values, SurveyFormatContext context)
        => string.Join(", ", values.Select(id => FindChoice(question, id) is { } choice ? (context?.UseValues == true ? choice.Value ?? choice.Id : choice.Text) : id));
}
