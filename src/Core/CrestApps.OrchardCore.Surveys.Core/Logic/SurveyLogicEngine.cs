using System.Globalization;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Core.Formulas;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Logic;

/// <summary>
/// Evaluates the visibility, enabled and required rules of a survey for a response on the server. A question on a
/// hidden page is hidden, and a hidden question answers nothing, so a chain of rules closes in one evaluation.
/// </summary>
public sealed class SurveyLogicEngine
{
    private readonly ISurveyControlRegistry _registry;
    private readonly IEnumerable<ISurveyConditionSourceProvider> _sourceProviders;
    private readonly SurveyFormulaService _formulas;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyLogicEngine"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    /// <param name="sourceProviders">The providers of context sources.</param>
    /// <param name="formulas">The formula service.</param>
    public SurveyLogicEngine(
        ISurveyControlRegistry registry,
        IEnumerable<ISurveyConditionSourceProvider> sourceProviders,
        SurveyFormulaService formulas)
    {
        _registry = registry;
        _sourceProviders = sourceProviders;
        _formulas = formulas;
    }

    /// <summary>
    /// Evaluates the logic of a definition for a response.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response, with its answers, variables and scores.</param>
    /// <param name="cacheKey">The id of the version, to cache compiled formulas, or <see langword="null"/>.</param>
    /// <returns>The state.</returns>
    public SurveyLogicState Evaluate(SurveyDefinition definition, SurveyResponse response, string cacheKey = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        var evaluation = new Evaluation(this, definition, response, cacheKey);

        return evaluation.Run();
    }

    /// <summary>
    /// Tells whether a rule and a formula hold for a response, with hidden questions answering nothing.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="state">The visibility state of the response.</param>
    /// <param name="rule">The rule, or <see langword="null"/>.</param>
    /// <param name="formula">The formula, or <see langword="null"/>.</param>
    /// <param name="cacheKey">The id of the version, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both hold (an empty rule and formula hold).</returns>
    public bool Holds(SurveyDefinition definition, SurveyResponse response, SurveyLogicState state, ConditionRule rule, string formula, string cacheKey = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        var evaluation = new Evaluation(this, definition, response, cacheKey);

        if (state is not null)
        {
            evaluation.UseState(state);
        }

        return evaluation.Condition(rule, formula, defaultWhenEmpty: true);
    }

    private sealed class Evaluation
    {
        private readonly SurveyLogicEngine _engine;
        private readonly SurveyDefinition _definition;
        private readonly SurveyResponse _response;
        private readonly string _cacheKey;
        private readonly Dictionary<string, SurveyQuestion> _questions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SurveyPage> _pageOfQuestion = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SurveyPage> _pages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _shown = new(StringComparer.Ordinal);
        private readonly HashSet<string> _visiting = new(StringComparer.Ordinal);
        private SurveyLogicState _fixedState;

        public Evaluation(SurveyLogicEngine engine, SurveyDefinition definition, SurveyResponse response, string cacheKey)
        {
            _engine = engine;
            _definition = definition;
            _response = response;
            _cacheKey = cacheKey;

            foreach (var page in definition.Pages ?? [])
            {
                if (string.IsNullOrEmpty(page?.Id))
                {
                    continue;
                }

                _pages[page.Id] = page;

                foreach (var question in page.Questions ?? [])
                {
                    if (!string.IsNullOrEmpty(question?.Id))
                    {
                        _questions[question.Id] = question;
                        _pageOfQuestion[question.Id] = page;
                    }
                }
            }
        }

        public void UseState(SurveyLogicState state)
            => _fixedState = state;

        public SurveyLogicState Run()
        {
            var state = new SurveyLogicState();

            foreach (var page in _pages.Values)
            {
                if (!PageShows(page))
                {
                    state.HiddenPageIds.Add(page.Id);
                }
            }

            foreach (var question in _questions.Values)
            {
                if (!QuestionShows(question))
                {
                    state.HiddenQuestionIds.Add(question.Id);

                    continue;
                }

                if (question.EnabledWhen is not null && !ConditionRule.IsEmpty(question.EnabledWhen) && !Condition(question.EnabledWhen, null, defaultWhenEmpty: true))
                {
                    state.DisabledQuestionIds.Add(question.Id);
                }

                var required = question.RequiredWhen is not null && !ConditionRule.IsEmpty(question.RequiredWhen)
                    ? Condition(question.RequiredWhen, null, defaultWhenEmpty: true)
                    : question.Required;

                if (required)
                {
                    state.RequiredQuestionIds.Add(question.Id);
                }

                foreach (var choice in question.Choices ?? [])
                {
                    if (choice is null || string.IsNullOrEmpty(choice.Id))
                    {
                        continue;
                    }

                    if (choice.Retired || !Shows(SurveyConditionSources.ChoiceKey(question.Id, choice.Id), choice.Visibility))
                    {
                        if (!state.HiddenChoiceIds.TryGetValue(question.Id, out var hidden))
                        {
                            hidden = new HashSet<string>(StringComparer.Ordinal);
                            state.HiddenChoiceIds[question.Id] = hidden;
                        }

                        hidden.Add(choice.Id);
                    }
                }
            }

            return state;
        }

        public bool Condition(ConditionRule rule, string formula, bool defaultWhenEmpty)
        {
            var hasRule = rule is not null && !ConditionRule.IsEmpty(rule);
            var hasFormula = !string.IsNullOrWhiteSpace(formula);

            if (!hasRule && !hasFormula)
            {
                return defaultWhenEmpty;
            }

            if (hasRule)
            {
                var judged = false;
                var met = ConditionEvaluator.IsMet(rule, source =>
                {
                    var value = Lookup(source);

                    if (value is not null)
                    {
                        judged = true;
                    }

                    return value;
                });

                // A rule that reads only unknown sources can't be judged: it holds only when the caller says an empty
                // condition holds, so a stale hide-when rule never hides.
                if (!judged)
                {
                    met = defaultWhenEmpty;
                }

                if (!met)
                {
                    return false;
                }
            }

            return !hasFormula || _engine._formulas.EvaluateBoolean(_definition, _response, formula, _cacheKey);
        }

        private bool PageShows(SurveyPage page)
            => Shows(SurveyConditionSources.PageKey(page.Id), page.Visibility);

        private bool QuestionShows(SurveyQuestion question)
        {
            if (_pageOfQuestion.TryGetValue(question.Id, out var page) && !PageShows(page))
            {
                return false;
            }

            // A question of a type that is not registered (its module is off) is treated as hidden for respondents.
            if (_engine._registry.GetHandler(question.ControlType) is null)
            {
                return false;
            }

            return Shows(SurveyConditionSources.QuestionKey(question.Id), question.Visibility);
        }

        private bool Shows(string key, SurveyVisibility visibility)
        {
            if (_fixedState is not null)
            {
                return true;
            }

            if (visibility is null || visibility.Mode == VisibilityMode.Always)
            {
                return true;
            }

            if (_shown.TryGetValue(key, out var known))
            {
                return known;
            }

            // A cycle shows, as an item without a rule would: the publish checks refuse cycles.
            if (!_visiting.Add(key))
            {
                return true;
            }

            bool result;

            if (visibility.Mode == VisibilityMode.ShowWhen)
            {
                result = Condition(visibility.Rule, visibility.Formula, defaultWhenEmpty: true);
            }
            else
            {
                result = !Condition(visibility.Rule, visibility.Formula, defaultWhenEmpty: false);
            }

            _visiting.Remove(key);
            _shown[key] = result;

            return result;
        }

        private bool IsHidden(SurveyQuestion question)
        {
            if (_fixedState is not null)
            {
                return _fixedState.HiddenQuestionIds.Contains(question.Id);
            }

            return !QuestionShows(question);
        }

        private ConditionValue Lookup(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            if (source.StartsWith(SurveyConditionSources.VariablePrefix, StringComparison.Ordinal))
            {
                var name = source.Substring(SurveyConditionSources.VariablePrefix.Length);

                return _response.Variables is not null && _response.Variables.TryGetValue(name, out var value)
                    ? ConditionValue.Of(value)
                    : _definition.Variables?.Any(variable => string.Equals(variable.Name, name, StringComparison.Ordinal)) == true ? ConditionValue.Empty : null;
            }

            if (string.Equals(source, SurveyConditionSources.Score, StringComparison.Ordinal))
            {
                return ConditionValue.Of(_response.Score?.ToString(CultureInfo.InvariantCulture));
            }

            if (source.StartsWith(SurveyConditionSources.ScorePrefix, StringComparison.Ordinal))
            {
                var category = source.Substring(SurveyConditionSources.ScorePrefix.Length);

                return _response.CategoryScores is not null && _response.CategoryScores.TryGetValue(category, out var score)
                    ? ConditionValue.Of(score.ToString(CultureInfo.InvariantCulture))
                    : ConditionValue.Empty;
            }

            if (source.StartsWith(SurveyConditionSources.ContextPrefix, StringComparison.Ordinal))
            {
                var key = source.Substring(SurveyConditionSources.ContextPrefix.Length);

                foreach (var provider in _engine._sourceProviders)
                {
                    if (provider.GetValue(_response, key) is { } value)
                    {
                        return value;
                    }
                }

                return null;
            }

            if (string.Equals(source, SurveyConditionSources.Collector, StringComparison.Ordinal))
            {
                return ConditionValue.Of(_response.CollectorId);
            }

            if (string.Equals(source, SurveyConditionSources.Culture, StringComparison.Ordinal))
            {
                return ConditionValue.Of(_response.Culture);
            }

            var questionId = source;
            string rowId = null;
            var dot = source.IndexOf('.', StringComparison.Ordinal);

            if (dot > 0)
            {
                questionId = source.Substring(0, dot);
                rowId = source.Substring(dot + 1);
            }

            if (!_questions.TryGetValue(questionId, out var question))
            {
                return null;
            }

            var handler = _engine._registry.GetHandler(question.ControlType);

            if (handler is null)
            {
                return null;
            }

            if (IsHidden(question))
            {
                return ConditionValue.Withheld;
            }

            _response.Answers.TryGetValue(question.Id, out var answer);

            return handler.GetConditionValue(question, answer, rowId) ?? ConditionValue.Empty;
        }
    }
}
