using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ConstantSum;

/// <summary>
/// Numbers for each item that must add up to a total. Each item's number is a row of the answer, written in the
/// invariant culture, posted as <c>q.{questionId}.{rowId}</c>.
/// </summary>
public sealed class ConstantSumControlHandler : SurveyControlHandlerBase<ConstantSumSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstantSumControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ConstantSumControlHandler(IStringLocalizer<ConstantSumControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.ConstantSum;

    /// <inheritdoc />
    public override void Initialize(SurveyQuestion question)
    {
        base.Initialize(question);

        if (question.Rows.Count == 0)
        {
            question.Rows.Add(new SurveySubQuestion { Id = IdGenerator.GenerateId(), Name = "item_1", Text = S["Item 1"] });
            question.Rows.Add(new SurveySubQuestion { Id = IdGenerator.GenerateId(), Name = "item_2", Text = S["Item 2"] });
        }
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rows = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var row in context.Question.Rows.Where(row => row is not null && !row.Retired))
        {
            var text = ReadValue(context, $"{context.Prefix}.{row.Id}");

            if (text is null)
            {
                continue;
            }

            if (TryParseNumber(text, out var number) && number >= 0)
            {
                rows[row.Id] = [number.ToString(CultureInfo.InvariantCulture)];
            }
            else
            {
                context.Errors.Add(S["Enter a number of 0 or more for {0}.", row.Text]);
            }
        }

        context.Answer = rows.Count == 0 ? null : new SurveyAnswer { Rows = rows };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Question.GetSettings<ConstantSumSettings>();

        if (IsEmpty(context.Question, context.Answer))
        {
            if (context.IsRequired)
            {
                context.Errors.Add(S["This question requires an answer."]);
            }

            return Task.CompletedTask;
        }

        if (Sum(context.Answer) != settings.Total)
        {
            context.Errors.Add(S["The numbers must add up to {0}.", settings.Total.ToString(CultureInfo.CurrentCulture)]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => !string.IsNullOrEmpty(rowId) && answer?.Rows is not null && answer.Rows.TryGetValue(rowId, out var values) && values is { Count: > 0 }
            && decimal.TryParse(values[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : null;

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => string.IsNullOrEmpty(rowId) ? DataFieldType.Text : DataFieldType.Decimal;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer?.Rows is not { Count: > 0 }
            ? string.Empty
            : string.Join("; ", question.Rows
                .Where(row => row is not null && answer.Rows.ContainsKey(row.Id))
                .Select(row => $"{row.Text}: {string.Join(string.Empty, answer.Rows[row.Id])}"));

    /// <inheritdoc />
    public override IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        foreach (var row in question.Rows.Where(row => row is not null))
        {
            yield return new SurveyExportColumn
            {
                Key = $"{question.Name}.{(string.IsNullOrWhiteSpace(row.Name) ? row.Id : row.Name)}",
                Title = $"{question.Title}: {row.Text}",
                QuestionId = question.Id,
                RowId = row.Id,
                Type = DataFieldType.Decimal,
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
            row[$"{question.Name}.{(string.IsNullOrWhiteSpace(sub.Name) ? sub.Id : sub.Name)}"] = GetFormulaValue(question, answer, sub.Id);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        foreach (var (rowId, _) in answer?.Rows ?? [])
        {
            if (GetFormulaValue(question, answer, rowId) is decimal number)
            {
                yield return new SurveyAnswerIndexRow { RowId = rowId, NumberValue = number };
            }
        }
    }

    // A sum too large for a decimal never adds up to the total: the answer is invalid instead of failing the post.
    private static decimal? Sum(SurveyAnswer answer)
    {
        try
        {
            return (answer?.Rows?.Values ?? Enumerable.Empty<List<string>>())
                .Sum(values => values is { Count: > 0 } && decimal.TryParse(values[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : 0);
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
