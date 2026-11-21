using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.MatrixDropdown;

/// <summary>
/// A grid with a drop-down in each cell. Each cell's answer is a row of the answer keyed <c>{rowId}:{columnId}</c>,
/// posted as <c>q.{questionId}.{rowId}.{columnId}</c>.
/// </summary>
public sealed class MatrixDropdownControlHandler : SurveyChoiceControlHandlerBase<MatrixDropdownSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixDropdownControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MatrixDropdownControlHandler(IStringLocalizer<MatrixDropdownControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.MatrixDropdown;

    /// <summary>
    /// Gets the answer row key of a cell.
    /// </summary>
    /// <param name="rowId">The row id.</param>
    /// <param name="columnId">The column id.</param>
    /// <returns>The key.</returns>
    public static string CellKey(string rowId, string columnId)
        => $"{rowId}:{columnId}";

    /// <inheritdoc />
    public override void Initialize(SurveyQuestion question)
    {
        base.Initialize(question);

        if (question.Rows.Count == 0)
        {
            question.Rows.Add(new SurveySubQuestion { Id = IdGenerator.GenerateId(), Name = "row_1", Text = S["Row 1"] });
        }

        var settings = question.GetSettings<MatrixDropdownSettings>();

        if (settings.Columns.Count == 0)
        {
            settings.Columns.Add(new SurveySubQuestion { Id = IdGenerator.GenerateId(), Name = "column_1", Text = S["Column 1"] });
            question.SetSettings(settings);
        }
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Question.Choices.Where(choice => choice is not null && !choice.Retired).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var settings = context.Question.GetSettings<MatrixDropdownSettings>();
        var rows = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var row in context.Question.Rows.Where(row => row is not null && !row.Retired))
        {
            foreach (var column in settings.Columns.Where(column => column is not null && !column.Retired))
            {
                var value = ReadValue(context, $"{context.Prefix}.{row.Id}.{column.Id}");

                if (value is not null && options.Contains(value))
                {
                    rows[CellKey(row.Id, column.Id)] = [value];
                }
            }
        }

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

        var settings = context.Question.GetSettings<MatrixDropdownSettings>();
        var filled = context.Answer?.Rows?.Count ?? 0;
        var cells = context.Question.Rows.Count(row => row is not null && !row.Retired) * settings.Columns.Count(column => column is not null && !column.Retired);

        if (filled == 0)
        {
            context.Errors.Add(S["This question requires an answer."]);
        }
        else if (settings.AllCellsRequired && filled < cells)
        {
            context.Errors.Add(S["Answer every cell."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => !string.IsNullOrEmpty(rowId) && answer?.Rows is not null && answer.Rows.TryGetValue(rowId, out var values) && values is { Count: > 0 }
            ? FindChoice(question, values[0])?.Value ?? values[0]
            : null;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        if (answer?.Rows is not { Count: > 0 })
        {
            return string.Empty;
        }

        var settings = question.GetSettings<MatrixDropdownSettings>();
        var parts = new List<string>();

        foreach (var row in question.Rows.Where(row => row is not null))
        {
            foreach (var column in settings.Columns.Where(column => column is not null))
            {
                if (answer.Rows.TryGetValue(CellKey(row.Id, column.Id), out var values) && values is { Count: > 0 })
                {
                    parts.Add($"{row.Text} / {column.Text}: {FindChoice(question, values[0])?.Text ?? values[0]}");
                }
            }
        }

        return string.Join("; ", parts);
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        var settings = question.GetSettings<MatrixDropdownSettings>();

        foreach (var row in question.Rows.Where(row => row is not null))
        {
            foreach (var column in settings.Columns.Where(column => column is not null))
            {
                yield return new SurveyExportColumn
                {
                    Key = $"{question.Name}.{Name(row)}.{Name(column)}",
                    Title = $"{question.Title}: {row.Text} / {column.Text}",
                    QuestionId = question.Id,
                    RowId = CellKey(row.Id, column.Id),
                };
            }
        }
    }

    /// <inheritdoc />
    public override void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(row);

        var settings = question.GetSettings<MatrixDropdownSettings>();

        foreach (var sub in question.Rows.Where(sub => sub is not null))
        {
            foreach (var column in settings.Columns.Where(column => column is not null))
            {
                var value = answer?.Rows is not null && answer.Rows.TryGetValue(CellKey(sub.Id, column.Id), out var values) && values is { Count: > 0 } ? values[0] : null;
                var choice = value is null ? null : FindChoice(question, value);

                row[$"{question.Name}.{Name(sub)}.{Name(column)}"] = choice is null ? value ?? string.Empty : context?.UseValues == true ? choice.Value ?? choice.Id : choice.Text;
            }
        }
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        foreach (var (key, values) in answer?.Rows ?? [])
        {
            foreach (var value in values ?? [])
            {
                yield return new SurveyAnswerIndexRow { RowId = Cut(key, 100), ChoiceId = value };
            }
        }
    }

    private static string Name(SurveySubQuestion item)
        => string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
}
