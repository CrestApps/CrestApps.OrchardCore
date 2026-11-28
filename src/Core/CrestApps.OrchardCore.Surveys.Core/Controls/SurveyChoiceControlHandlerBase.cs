using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// A handler for questions that pick from choices: choice order (shuffled with pinned choices in place, other and
/// exclusive choices last), carried-forward choices, the other text, exclusive choices and one index row per chosen
/// choice. Answers hold choice ids.
/// </summary>
/// <typeparam name="TSettings">The type of the control's settings.</typeparam>
public abstract class SurveyChoiceControlHandlerBase<TSettings> : SurveyControlHandlerBase<TSettings>
    where TSettings : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyChoiceControlHandlerBase{TSettings}"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    protected SurveyChoiceControlHandlerBase(IStringLocalizer stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <summary>
    /// Gets whether the respondent may pick more than one choice.
    /// </summary>
    protected virtual bool AllowsMultiple => false;

    /// <inheritdoc />
    public override void Initialize(SurveyQuestion question)
    {
        base.Initialize(question);

        if (question.Choices.Count == 0)
        {
            question.Choices.Add(new SurveyChoice { Id = IdGenerator.GenerateId(), Value = "1", Text = S["Option 1"] });
            question.Choices.Add(new SurveyChoice { Id = IdGenerator.GenerateId(), Value = "2", Text = S["Option 2"] });
        }
    }

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        var choices = SurveyChoiceSource.GetChoices(question, context.Definition);

        if (string.IsNullOrEmpty(question.ChoicesFrom) && !choices.Any(choice => !choice.Retired))
        {
            context.AddError("no-choices", S["Question {0} needs at least one choice.", question.Name], question.Id);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var choice in question.Choices)
        {
            if (string.IsNullOrEmpty(choice?.Id) || !ids.Add(choice.Id))
            {
                context.AddError("choice-id", S["Every choice of question {0} needs its own id.", question.Name], question.Id);

                break;
            }
        }
    }

    /// <inheritdoc />
    public override void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(context);

        context.Plan.ChoiceOrder[question.Id] = SurveyChoiceSource.OrderChoices(SurveyChoiceSource.GetChoices(question, context.Definition), question.ShuffleChoices, context.Random);
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var choices = SurveyChoiceSource.GetChoices(context.Question, context.Definition);
        var allowed = AllowedIds(context.AllowedChoiceIds, choices);
        var chosen = ReadValues(context, context.Prefix)
            .Where(allowed.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!AllowsMultiple && chosen.Count > 1)
        {
            chosen = [chosen[0]];
        }

        var exclusive = chosen.FirstOrDefault(id => choices.Any(choice => choice.Id == id && choice.IsExclusive));

        if (exclusive is not null)
        {
            chosen = [exclusive];
        }

        if (chosen.Count == 0)
        {
            context.Answer = null;

            return Task.CompletedTask;
        }

        var answer = new SurveyAnswer { Values = chosen };

        if (chosen.Any(id => choices.Any(choice => choice.Id == id && choice.IsOther)))
        {
            answer.OtherText = Cut(ReadValue(context, context.Prefix + ".other"), 1000);
        }

        context.Answer = answer;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsEmpty(context.Question, context.Answer))
        {
            if (context.IsRequired)
            {
                context.Errors.Add(AllowsMultiple ? S["Select at least one option."] : S["Select an option."]);
            }

            return Task.CompletedTask;
        }

        var choices = SurveyChoiceSource.GetChoices(context.Question, context.Definition);
        var allowed = AllowedIds(context.AllowedChoiceIds, choices);

        if (context.Answer.Values.Any(id => !allowed.Contains(id)))
        {
            context.Errors.Add(S["Select one of the options shown."]);
        }

        if (context.Answer.Values.Any(id => choices.Any(choice => choice.Id == id && choice.IsOther)) && string.IsNullOrWhiteSpace(context.Answer.OtherText))
        {
            context.Errors.Add(S["Describe your other answer."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
        => answer is null || !string.IsNullOrEmpty(rowId) ? base.GetConditionValue(question, answer, rowId) : new ConditionValue(answer.Values ?? []);

    /// <inheritdoc />
    public override object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId)
    {
        if (answer?.Values is not { Count: > 0 } values)
        {
            return null;
        }

        var codes = values.Select(id => FindChoice(question, id)?.Value ?? id).ToList();

        return AllowsMultiple ? string.Join(", ", codes) : codes[0];
    }

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        if (answer?.Values is not { Count: > 0 } values)
        {
            return string.Empty;
        }

        var texts = new List<string>();

        foreach (var id in values)
        {
            var choice = FindChoice(question, context?.Definition, id);

            if (choice is null)
            {
                texts.Add(id);

                continue;
            }

            var text = context?.UseValues == true ? choice.Value ?? choice.Id : context?.Text($"choice:{question.Id}:{choice.Id}", choice.Text) ?? choice.Text;

            if (choice.IsOther && !string.IsNullOrWhiteSpace(answer.OtherText))
            {
                text = $"{text}: {answer.OtherText}";
            }

            texts.Add(text);
        }

        return string.Join(", ", texts);
    }

    /// <inheritdoc />
    public override IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer)
    {
        foreach (var id in answer?.Values ?? [])
        {
            var choice = FindChoice(question, id);

            yield return new SurveyAnswerIndexRow
            {
                ChoiceId = id,
                TextValue = choice?.IsOther == true ? Cut(answer.OtherText, 255) : null,
            };
        }
    }

    /// <summary>
    /// Finds a choice of a question by id among the question's own choices. A caller that has no definition at hand is
    /// given a question whose choices were resolved (<see cref="SurveyChoiceSource.WithSourceChoices"/>).
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="choiceId">The choice id.</param>
    /// <returns>The choice, or <see langword="null"/>.</returns>
    protected static SurveyChoice FindChoice(SurveyQuestion question, string choiceId)
        => FindChoice(question, definition: null, choiceId);

    /// <summary>
    /// Finds a choice of a question by id, following carried-forward choices to the question they come from.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="definition">The definition the question belongs to, or <see langword="null"/>.</param>
    /// <param name="choiceId">The choice id.</param>
    /// <returns>The choice, or <see langword="null"/>.</returns>
    protected static SurveyChoice FindChoice(SurveyQuestion question, SurveyDefinition definition, string choiceId)
        => SurveyChoiceSource.GetChoices(question, definition).FirstOrDefault(choice => string.Equals(choice?.Id, choiceId, StringComparison.Ordinal));

    /// <summary>
    /// Gets the choices that may be picked: the list given, even an empty one (a carried-forward question with nothing
    /// carried forward allows nothing, it never falls back to every choice); every choice not retired when none is given.
    /// </summary>
    /// <param name="allowed">The choice ids the page offers, or <see langword="null"/>.</param>
    /// <param name="choices">The question's choices.</param>
    /// <returns>The choice ids.</returns>
    protected static HashSet<string> AllowedIds(IReadOnlyList<string> allowed, IEnumerable<SurveyChoice> choices)
        => allowed is not null
            ? allowed.ToHashSet(StringComparer.Ordinal)
            : choices.Where(choice => choice is not null && !choice.Retired).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
}
