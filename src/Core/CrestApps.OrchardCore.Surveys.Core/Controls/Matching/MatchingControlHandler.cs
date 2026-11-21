using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Matching;

/// <summary>
/// Prompts matched to answers. Each prompt's answer holds one answer (choice) id.
/// </summary>
public sealed class MatchingControlHandler : SurveyChoiceControlHandlerBase<MatchingSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatchingControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public MatchingControlHandler(IStringLocalizer<MatchingControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Matching;

    /// <inheritdoc />
    public override void Initialize(SurveyQuestion question)
    {
        base.Initialize(question);

        if (question.Rows.Count == 0)
        {
            question.Rows.Add(new SurveySubQuestion { Id = IdGenerator.GenerateId(), Name = "prompt_1", Text = S["Prompt 1"] });
            question.Rows.Add(new SurveySubQuestion { Id = IdGenerator.GenerateId(), Name = "prompt_2", Text = S["Prompt 2"] });
        }
    }

    /// <inheritdoc />
    public override void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(context);

        var settings = question.GetSettings<MatchingSettings>();

        context.Plan.ChoiceOrder[question.Id] = SurveyChoiceSource.OrderChoices(question.Choices, settings.ShuffleAnswers, context.Random);
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var answers = context.Question.Choices.Where(choice => choice is not null && !choice.Retired).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var rows = SurveyRowAnswers.Read(context, answers, multiple: false);

        context.Answer = rows.Count == 0 ? null : new SurveyAnswer { Rows = rows };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var answered = context.Answer?.Rows?.Count ?? 0;
        var prompts = context.Question.Rows.Count(row => row is not null && !row.Retired);

        if (context.IsRequired && answered < prompts)
        {
            context.Errors.Add(S["Match every prompt."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => !string.IsNullOrEmpty(rowId) && answer?.Rows is not null && answer.Rows.TryGetValue(rowId, out var values)
            ? new ConditionValue(values ?? [])
            : ConditionValue.Empty;

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => !string.IsNullOrEmpty(rowId) && answer?.Rows is not null && answer.Rows.TryGetValue(rowId, out var values) && values?.Count > 0
            ? FindChoice(question, values[0])?.Value ?? values[0]
            : null;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        if (answer?.Rows is not { Count: > 0 })
        {
            return string.Empty;
        }

        return string.Join("; ", question.Rows
            .Where(row => row is not null && answer.Rows.ContainsKey(row.Id))
            .Select(row => $"{row.Text} = {string.Join(", ", answer.Rows[row.Id].Select(id => FindChoice(question, id)?.Text ?? id))}"));
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
}
