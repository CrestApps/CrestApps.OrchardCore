using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.MultipleText;

/// <summary>
/// Several short texts, one per item. Each item's text is a row of the answer, posted as
/// <c>q.{questionId}.{rowId}</c>.
/// </summary>
public sealed class MultipleTextControlHandler : SurveyControlHandlerBase<MultipleTextSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipleTextControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MultipleTextControlHandler(IStringLocalizer<MultipleTextControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.MultipleText;

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

        var settings = context.Question.GetSettings<MultipleTextSettings>();
        var max = settings.MaxLength is > 0 ? settings.MaxLength.Value : 500;
        var rows = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var row in context.Question.Rows.Where(row => row is not null && !row.Retired))
        {
            var value = ReadValue(context, $"{context.Prefix}.{row.Id}");

            if (value is not null)
            {
                rows[row.Id] = [Cut(value, max)];
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

        var settings = context.Question.GetSettings<MultipleTextSettings>();
        var filled = context.Answer?.Rows?.Count ?? 0;
        var items = context.Question.Rows.Count(row => row is not null && !row.Retired);

        if (filled == 0)
        {
            context.Errors.Add(S["This question requires an answer."]);
        }
        else if (settings.AllItemsRequired && filled < items)
        {
            context.Errors.Add(S["Fill in every item."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer?.Rows is not { Count: > 0 }
            ? string.Empty
            : string.Join("; ", question.Rows
                .Where(row => row is not null && answer.Rows.ContainsKey(row.Id))
                .Select(row => $"{row.Text}: {string.Join(", ", answer.Rows[row.Id])}"));

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
            row[$"{question.Name}.{(string.IsNullOrWhiteSpace(sub.Name) ? sub.Id : sub.Name)}"] =
                answer?.Rows is not null && answer.Rows.TryGetValue(sub.Id, out var values) ? string.Join(", ", values ?? []) : string.Empty;
        }
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        foreach (var (rowId, values) in answer?.Rows ?? [])
        {
            foreach (var value in values ?? [])
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return new SurveyAnswerIndexRow { RowId = rowId, TextValue = Cut(value, 255) };
                }
            }
        }
    }
}
