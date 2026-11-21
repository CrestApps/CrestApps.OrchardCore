using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;

/// <summary>
/// A text with blanks filled from a pool of words. The answer holds the word picked for each blank, in order (inputs
/// <c>q.{questionId}.{index}</c>). The pool is sorted alphabetically so its order never gives the key away.
/// </summary>
public sealed class ChoiceBlanksControlHandler : SurveyControlHandlerBase<ChoiceBlanksSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChoiceBlanksControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ChoiceBlanksControlHandler(IStringLocalizer<ChoiceBlanksControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.ChoiceBlanks;

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        if (SurveyBlankText.Parse(question.GetSettings<ChoiceBlanksSettings>().Text).Answers.Count == 0)
        {
            context.AddError("no-blanks", S["The text of question {0} needs at least one blank written as [[word]].", question.Name], question.Id);
        }
    }

    /// <inheritdoc />
    public override JsonObject GetRespondentSettings(SurveyQuestion question)
    {
        var settings = question.GetSettings<ChoiceBlanksSettings>();
        var (segments, answers) = SurveyBlankText.Parse(settings.Text);

        return new JsonObject
        {
            ["Segments"] = new JsonArray(segments.Select(segment => (JsonNode)segment).ToArray()),
            ["BlankCount"] = answers.Count,
            ["Words"] = new JsonArray(Pool(settings).Select(word => (JsonNode)word).ToArray()),
            ["Display"] = settings.Display.ToString(),
        };
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Question.GetSettings<ChoiceBlanksSettings>();
        var count = SurveyBlankText.Parse(settings.Text).Answers.Count;
        var pool = Pool(settings).ToHashSet(StringComparer.Ordinal);
        var values = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var value = SurveyBlankText.First(SurveyRowAnswers.ReadRaw(context, $"{context.Prefix}.{i}"))?.Trim();

            values.Add(value is not null && pool.Contains(value) ? value : string.Empty);
        }

        context.Answer = values.All(string.IsNullOrWhiteSpace) ? null : new SurveyAnswer { Values = values };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsRequired && (context.Answer?.Values?.Any(string.IsNullOrWhiteSpace) ?? true))
        {
            context.Errors.Add(S["Fill in every blank."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
        => answer?.Values is { Count: > 0 } values ? string.Join(" / ", values) : string.Empty;

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        var values = answer?.Values ?? [];

        for (var i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                yield return new SurveyAnswerIndexRow { RowId = $"b{i}", TextValue = Cut(values[i], 255) };
            }
        }
    }

    /// <summary>
    /// Gets the pool of words: the first answer of each blank and the distractors, sorted.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The words.</returns>
    public static IReadOnlyList<string> Pool(ChoiceBlanksSettings settings)
        => SurveyBlankText.Parse(settings?.Text).Answers
            .Select(answers => SurveyBlankText.First(answers))
            .Concat(settings?.Distractors ?? [])
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(word => word, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
