using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;

/// <summary>
/// A text with blanks to type into. The answer holds one value per blank, in order (blank inputs are
/// <c>q.{questionId}.{index}</c>); respondents see the text's segments without the answers.
/// </summary>
public sealed class FillInBlanksControlHandler : SurveyControlHandlerBase<FillInBlanksSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FillInBlanksControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public FillInBlanksControlHandler(IStringLocalizer<FillInBlanksControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.FillInBlanks;

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        if (SurveyBlankText.Parse(question.GetSettings<FillInBlanksSettings>().Text).Answers.Count == 0)
        {
            context.AddError("no-blanks", S["The text of question {0} needs at least one blank written as [[answer]].", question.Name], question.Id);
        }
    }

    /// <inheritdoc />
    public override JsonObject GetRespondentSettings(SurveyQuestion question)
    {
        var (segments, answers) = SurveyBlankText.Parse(question.GetSettings<FillInBlanksSettings>().Text);

        return new JsonObject
        {
            ["Segments"] = new JsonArray(segments.Select(segment => (JsonNode)segment).ToArray()),
            ["BlankCount"] = answers.Count,
        };
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var count = SurveyBlankText.Parse(context.Question.GetSettings<FillInBlanksSettings>().Text).Answers.Count;
        var values = new List<string>();

        for (var i = 0; i < count; i++)
        {
            values.Add(Cut(SurveyBlankText.First(SurveyRowAnswers.ReadRaw(context, $"{context.Prefix}.{i}"))?.Trim() ?? string.Empty, 500));
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
}
