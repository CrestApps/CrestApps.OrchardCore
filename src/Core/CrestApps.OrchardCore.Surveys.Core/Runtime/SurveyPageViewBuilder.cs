using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Translations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Builds what a respondent sees of the current screen: the questions in plan order with piped texts, the choices that
/// show, no key, and the rules the browser may evaluate without a round trip.
/// </summary>
public sealed class SurveyPageViewBuilder
{
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyPipingRenderer _piping;
    private readonly SurveyRespondentProjector _projector;
    private readonly SurveyDefinitionLocalizer _localizer;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPageViewBuilder"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    /// <param name="piping">The piping renderer.</param>
    /// <param name="projector">The respondent projector.</param>
    /// <param name="localizer">The definition localizer, for translated texts.</param>
    /// <param name="clock">The clock, for the time limits of pages.</param>
    public SurveyPageViewBuilder(
        ISurveyControlRegistry registry,
        SurveyPipingRenderer piping,
        SurveyRespondentProjector projector,
        SurveyDefinitionLocalizer localizer,
        IClock clock)
    {
        _localizer = localizer;
        _clock = clock;
        _registry = registry;
        _piping = piping;
        _projector = projector;
    }

    /// <summary>
    /// Builds the view of a screen.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The logic state.</param>
    /// <param name="screenId">The screen.</param>
    /// <returns>The view.</returns>
    public SurveyPageView Build(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, string screenId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(state);

        definition = _localizer.Localize(definition, response.Culture);

        var plan = response.Plan;
        var pageId = SurveyPlanNavigator.PageOf(plan, screenId);
        var page = definition.Pages?.FirstOrDefault(item => string.Equals(item.Id, pageId, StringComparison.Ordinal)) ?? new SurveyPage { Id = pageId };
        var questions = (definition.Pages ?? []).SelectMany(item => item.Questions ?? []).Where(question => !string.IsNullOrEmpty(question?.Id)).GroupBy(question => question.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var screenQuestionIds = SurveyPlanNavigator.QuestionsOf(plan, screenId).Where(questions.ContainsKey).ToList();
        var onScreen = screenQuestionIds.ToHashSet(StringComparer.Ordinal);
        var showing = plan.PageOrder.Where(screen => SurveyPlanNavigator.Shows(plan, screen, state)).ToList();
        var position = Math.Max(showing.IndexOf(screenId), 0) + 1;
        var hiddenQuestions = new HashSet<string>(state.HiddenQuestionIds, StringComparer.Ordinal);
        var view = new SurveyPageView
        {
            Id = screenId,
            // A page title is plain text (the page encodes it); its description is HTML, so piped values are encoded.
            Title = _piping.Render(page.Title, definition, response, hiddenQuestions, encode: false),
            Description = _piping.Render(page.Description, definition, response, hiddenQuestions),
            Position = position,
            PageCount = Math.Max(showing.Count, 1),
            DeadlineUtc = SurveyPageTimer.Deadline(definition, response),
        };

        var number = NumberBefore(definition, response, state, screenId, questions);

        foreach (var questionId in screenQuestionIds)
        {
            var question = questions[questionId];
            var handler = _registry.GetHandler(question.ControlType);

            if (handler is null)
            {
                continue;
            }

            var descriptor = _registry.GetControl(question.ControlType);
            var answerable = descriptor?.Has(SurveyControlCapabilities.Answerable) ?? true;
            // A carried-forward question with nothing carried forward has no option to pick: it is hidden (and not required),
            // rather than a required question with no options that blocks the page for good.
            var hidden = state.HiddenQuestionIds.Contains(question.Id) || NothingCarriedForward(definition, question, response, state);

            if (hidden)
            {
                view.HiddenQuestionIds.Add(question.Id);
            }

            int? shownNumber = null;

            if (answerable && definition.Presentation?.ShowQuestionNumbers != false && !hidden)
            {
                number++;
                shownNumber = number;
            }

            view.Questions.Add(Project(definition, response, state, question, handler, shownNumber, hiddenQuestions));

            if (state.HiddenChoiceIds.TryGetValue(question.Id, out var hiddenChoices) && hiddenChoices.Count > 0)
            {
                view.HiddenChoiceIds[question.Id] = hiddenChoices.ToList();
            }

            AddClientRule(view, SurveyConditionSources.QuestionKey(question.Id), question.Visibility, onScreen);

            foreach (var choice in question.Choices ?? [])
            {
                if (!string.IsNullOrEmpty(choice?.Id) && !choice.Retired)
                {
                    AddClientRule(view, SurveyConditionSources.ChoiceKey(question.Id, choice.Id), choice.Visibility, onScreen);
                }
            }

            if (PipesSameScreen(question, definition, onScreen, questions))
            {
                view.ServerRuleKeys.Add("text:" + question.Id);
            }
        }

        var index = plan.PageOrder.IndexOf(screenId);
        view.IsLast = index < 0 || SurveyPlanNavigator.FirstShowing(plan, state, index + 1) is null;
        view.CanGoBack = response.PagePath.Count > 1
            && definition.Navigation?.AllowBack != false
            && definition.Navigation?.Sequential != true
            && page.AllowBack
            && !SurveyPageTimer.ScreenTimeIsUp(definition, response, response.PagePath[response.PagePath.Count - 2], _clock.UtcNow);

        view.ProgressPercent = ProgressPercent(definition, response, state, showing, screenId, questions);

        return view;
    }

    /// <summary>
    /// Projects a question for respondents: piped texts, settings without the key, visible choices in plan order, rows
    /// that are not retired. The scoring key is never copied.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The logic state.</param>
    /// <param name="question">The question.</param>
    /// <param name="handler">The handler of its type.</param>
    /// <param name="number">The number shown, or <see langword="null"/>.</param>
    /// <param name="hiddenQuestions">The hidden questions, which pipe as empty.</param>
    /// <returns>The view.</returns>
    public SurveyQuestionView Project(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, SurveyQuestion question, ISurveyControlHandler handler, int? number, ISet<string> hiddenQuestions)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(handler);

        definition = _localizer.Localize(definition, response?.Culture);
        question = definition?.Pages?.SelectMany(page => page.Questions ?? []).FirstOrDefault(item => string.Equals(item?.Id, question.Id, StringComparison.Ordinal)) ?? question;

        var allowed = AllowedChoices(definition, question, response, state).ToHashSet(StringComparer.Ordinal);
        var hiddenChoices = SurveyChoiceSource.GetChoices(question, definition)
            .Where(choice => !string.IsNullOrEmpty(choice?.Id) && !allowed.Contains(choice.Id))
            .Select(choice => choice.Id)
            .ToList();

        // A title and a description are HTML (shown sanitized), so piped values are HTML-encoded in them. Choice and row
        // texts and placeholders are plain text that the page encodes itself: encoding the values there too would show
        // O'Brien as O&#39;Brien.
        var view = _projector.Project(question, definition, response.Plan, hiddenChoices, (key, text) => _piping.Render(text, definition, response, hiddenQuestions, encode: IsHtmlText(key)));

        view.Number = number;
        view.Required = state?.RequiredQuestionIds.Contains(question.Id) ?? question.Required;
        view.Disabled = state?.DisabledQuestionIds.Contains(question.Id) ?? false;

        return view;
    }

    /// <summary>
    /// Gets the choice ids a respondent may pick for a question: plan order, shown, not retired, and, for carried-forward
    /// choices, those the source answer allows.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="question">The question.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The logic state.</param>
    /// <returns>The choice ids.</returns>
    public static IReadOnlyList<string> AllowedChoices(SurveyDefinition definition, SurveyQuestion question, SurveyResponse response, SurveyLogicState state)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(response);

        var choices = SurveyChoiceSource.GetChoices(question, definition);
        var order = response.Plan.ChoiceOrder.TryGetValue(question.Id, out var planned)
            ? planned
            : SurveyChoiceSource.OrderChoices(choices, shuffle: false, random: null);
        var retired = choices.Where(choice => choice?.Retired == true).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var known = choices.Where(choice => !string.IsNullOrEmpty(choice?.Id)).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);
        var shown = order.Where(id => known.Contains(id) && !retired.Contains(id) && (state is null || !state.IsChoiceHidden(question.Id, id)));

        response.Answers.TryGetValue(question.ChoicesFrom ?? string.Empty, out var sourceAnswer);

        return SurveyChoiceSource.FilterCarriedForward(question, shown, sourceAnswer);
    }

    /// <summary>
    /// Gets whether a question carries its choices forward from another answer and nothing is carried forward, so it has
    /// no option to pick: it is hidden and its requirement doesn't apply.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="question">The question.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The logic state.</param>
    /// <returns><see langword="true"/> when nothing is carried forward.</returns>
    public static bool NothingCarriedForward(SurveyDefinition definition, SurveyQuestion question, SurveyResponse response, SurveyLogicState state)
        => !string.IsNullOrEmpty(question?.ChoicesFrom)
            && question.ChoicesFromMode != ChoicesFromMode.All
            && AllowedChoices(definition, question, response, state).Count == 0;

    /// <summary>
    /// Gets whether a text of a question is HTML, by its text key: its title and its description. Choice texts, row
    /// texts and placeholders are plain text.
    /// </summary>
    /// <param name=key>The text key, such as <c>title:{id}</c>.</param>
    /// <returns><see langword="true"/> for an HTML text.</returns>
    public static bool IsHtmlText(string key)
        => key is not null && (key.StartsWith("title:", StringComparison.Ordinal) || key.StartsWith("description:", StringComparison.Ordinal));

    private static void AddClientRule(SurveyPageView view, string key, SurveyVisibility visibility, HashSet<string> onScreen)
    {
        if (visibility is null || visibility.Mode == VisibilityMode.Always)
        {
            return;
        }

        var sources = ConditionalVisibility.Sources(visibility.Rule).ToList();
        var readsScreen = sources.Any(source => onScreen.Contains(QuestionIdOf(source)));

        if (!readsScreen && string.IsNullOrWhiteSpace(visibility.Formula))
        {
            // The rule reads only earlier answers: it cannot change on this screen, and the server already applied it.
            return;
        }

        var plain = visibility.Mode == VisibilityMode.ShowWhen
            && string.IsNullOrWhiteSpace(visibility.Formula)
            && sources.All(source => onScreen.Contains(QuestionIdOf(source)));

        if (plain)
        {
            view.ClientRules[key] = visibility.Rule;
        }
        else
        {
            view.ServerRuleKeys.Add(key);
        }
    }

    private static string QuestionIdOf(string source)
    {
        var dot = source.IndexOf('.', StringComparison.Ordinal);

        return dot > 0 ? source.Substring(0, dot) : source;
    }

    private static bool PipesSameScreen(SurveyQuestion question, SurveyDefinition definition, HashSet<string> onScreen, Dictionary<string, SurveyQuestion> questions)
    {
        var names = onScreen.Select(id => questions[id].Name).Where(name => !string.IsNullOrEmpty(name)).ToHashSet(StringComparer.Ordinal);
        var texts = new[] { question.Title, question.Description }.Concat((question.Choices ?? []).Select(choice => choice?.Text));

        return texts.SelectMany(SurveyPipingRenderer.References).Any(reference =>
        {
            var name = reference.Split('.')[0];

            return names.Contains(name) || reference.StartsWith("var:", StringComparison.Ordinal) || definition.Variables?.Any(variable => variable.Kind == VariableKind.Calculated && string.Equals(variable.Name, name, StringComparison.Ordinal)) == true;
        });
    }

    private int NumberBefore(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, string screenId, Dictionary<string, SurveyQuestion> questions)
    {
        var number = 0;

        foreach (var screen in response.Plan.PageOrder)
        {
            if (string.Equals(screen, screenId, StringComparison.Ordinal))
            {
                break;
            }

            if (!SurveyPlanNavigator.Shows(response.Plan, screen, state))
            {
                continue;
            }

            foreach (var id in SurveyPlanNavigator.QuestionsOf(response.Plan, screen))
            {
                if (questions.TryGetValue(id, out var question)
                    && !state.HiddenQuestionIds.Contains(id)
                    && _registry.GetControl(question.ControlType)?.Has(SurveyControlCapabilities.Answerable) == true)
                {
                    number++;
                }
            }
        }

        return number;
    }

    private int ProgressPercent(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, List<string> showing, string screenId, Dictionary<string, SurveyQuestion> questions)
    {
        if (showing.Count == 0)
        {
            return 0;
        }

        if (definition.Presentation?.ProgressBasis == ProgressBasis.Questions)
        {
            var visible = showing
                .SelectMany(screen => SurveyPlanNavigator.QuestionsOf(response.Plan, screen))
                .Where(id => questions.TryGetValue(id, out var question) && !state.HiddenQuestionIds.Contains(id) && _registry.GetControl(question.ControlType)?.Has(SurveyControlCapabilities.Answerable) == true)
                .ToList();

            if (visible.Count == 0)
            {
                return 0;
            }

            var answered = visible.Count(id => response.Answers.ContainsKey(id));

            return (int)Math.Round(answered * 100m / visible.Count);
        }

        var index = Math.Max(showing.IndexOf(screenId), 0);

        return (int)Math.Round(index * 100m / showing.Count);
    }
}
