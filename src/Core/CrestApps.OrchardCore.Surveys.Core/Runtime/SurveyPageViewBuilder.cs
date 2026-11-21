using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Builds what a respondent sees of the current screen: the questions in plan order with piped texts, the choices that
/// show, no key, and the rules the browser may evaluate without a round trip.
/// </summary>
public sealed class SurveyPageViewBuilder
{
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyPipingRenderer _piping;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPageViewBuilder"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    /// <param name="piping">The piping renderer.</param>
    public SurveyPageViewBuilder(ISurveyControlRegistry registry, SurveyPipingRenderer piping)
    {
        _registry = registry;
        _piping = piping;
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
            Title = _piping.Render(page.Title, definition, response, hiddenQuestions),
            Description = _piping.Render(page.Description, definition, response, hiddenQuestions),
            Position = position,
            PageCount = Math.Max(showing.Count, 1),
            DeadlineUtc = response.DeadlineUtc is { } deadline ? DateTime.SpecifyKind(deadline, DateTimeKind.Utc) : null,
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
            var hidden = state.HiddenQuestionIds.Contains(question.Id);

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
            && page.AllowBack;

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

        var view = new SurveyQuestionView
        {
            Id = question.Id,
            Name = question.Name,
            ControlType = question.ControlType,
            Number = number,
            Title = _piping.Render(question.Title, definition, response, hiddenQuestions),
            Description = _piping.Render(question.Description, definition, response, hiddenQuestions),
            Placeholder = question.Placeholder,
            Required = state?.RequiredQuestionIds.Contains(question.Id) ?? question.Required,
            Disabled = state?.DisabledQuestionIds.Contains(question.Id) ?? false,
            Settings = handler.GetRespondentSettings(question) ?? [],
        };

        var choices = (question.Choices ?? []).Where(choice => !string.IsNullOrEmpty(choice?.Id)).ToDictionary(choice => choice.Id, StringComparer.Ordinal);
        var order = response.Plan.ChoiceOrder.TryGetValue(question.Id, out var planned)
            ? planned
            : SurveyPlanBuilder.DefaultChoiceOrder(question, null);

        foreach (var choiceId in order)
        {
            if (!choices.TryGetValue(choiceId, out var choice) || choice.Retired)
            {
                continue;
            }

            view.Choices.Add(new SurveyChoiceView
            {
                Id = choice.Id,
                Value = choice.Value,
                Text = _piping.Render(choice.Text, definition, response, hiddenQuestions),
                ImageUrl = choice.ImageUrl,
                IsOther = choice.IsOther,
                IsExclusive = choice.IsExclusive,
            });
        }

        foreach (var row in question.Rows ?? [])
        {
            if (!string.IsNullOrEmpty(row?.Id) && !row.Retired)
            {
                view.Rows.Add(new SurveySubQuestionView { Id = row.Id, Name = row.Name, Text = row.Text });
            }
        }

        return view;
    }

    /// <summary>
    /// Gets the choice ids a respondent may pick for a question: plan order, shown, not retired.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The logic state.</param>
    /// <returns>The choice ids.</returns>
    public static IReadOnlyList<string> AllowedChoices(SurveyQuestion question, SurveyResponse response, SurveyLogicState state)
    {
        var order = response.Plan.ChoiceOrder.TryGetValue(question.Id, out var planned)
            ? planned
            : SurveyPlanBuilder.DefaultChoiceOrder(question, null);
        var retired = (question.Choices ?? []).Where(choice => choice?.Retired == true).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal);

        return order.Where(id => !retired.Contains(id) && (state is null || !state.IsChoiceHidden(question.Id, id))).ToList();
    }

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
