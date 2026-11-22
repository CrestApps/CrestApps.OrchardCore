using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Checks a survey definition before it is published: ids and names, question types, logic, branches, endings,
/// patterns, pools, scoring, piping and formulas, then every question type's own checks and every contributor's.
/// </summary>
public sealed class SurveyDefinitionValidator : ISurveyDefinitionValidator
{
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex _piping = new(@"(?<!\{)\{([A-Za-z_][A-Za-z0-9_]*(?:[.:][A-Za-z0-9_]+)?)\}(?!\})", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly ISurveyControlRegistry _registry;
    private readonly IEnumerable<ISurveyDefinitionValidationContributor> _contributors;
    private readonly ISurveyFormulaValidator _formulaValidator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyDefinitionValidator"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    /// <param name="contributors">The contributors of checks.</param>
    /// <param name="formulaValidator">The formula validator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyDefinitionValidator(
        ISurveyControlRegistry registry,
        IEnumerable<ISurveyDefinitionValidationContributor> contributors,
        ISurveyFormulaValidator formulaValidator,
        IStringLocalizer<SurveyDefinitionValidator> stringLocalizer)
    {
        _registry = registry;
        _contributors = contributors;
        _formulaValidator = formulaValidator;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<SurveyDefinitionValidationContext> ValidateAsync(Survey survey, SurveyDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var context = new SurveyDefinitionValidationContext(survey, definition);
        var map = new DefinitionMap(definition);

        CheckIdsAndNames(context, map);
        CheckQuestions(context, map);
        CheckLogic(context, map);
        CheckBranchesAndEndings(context, map);
        CheckPools(context);
        CheckScoring(context, map);
        CheckPiping(context, map);
        CheckFormulas(context);

        foreach (var (_, _, question) in SurveyDefinitions.Questions(definition))
        {
            var handler = _registry.GetHandler(question.ControlType);

            if (handler is not null)
            {
                await handler.ValidateDefinitionAsync(question, context);
            }
        }

        foreach (var contributor in _contributors)
        {
            await contributor.ValidateAsync(context, cancellationToken);
        }

        return context;
    }

    private void CheckIdsAndNames(SurveyDefinitionValidationContext context, DefinitionMap map)
    {
        var definition = context.Definition;
        var ids = new HashSet<string>(StringComparer.Ordinal);

        void Id(string id, string what, string questionId = null, string pageId = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                context.AddError("missing-id", S["A {0} has no id.", what], questionId, pageId);
            }
            else if (!ids.Add(id))
            {
                context.AddError("duplicate-id", S["The id {0} is used more than once.", id], questionId, pageId);
            }
        }

        var pageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in definition.Pages)
        {
            Id(page.Id, S["page"], pageId: page.Id);

            if (!string.IsNullOrEmpty(page.Name) && !pageNames.Add(page.Name))
            {
                context.AddError("duplicate-page-name", S["The page code {0} is used more than once.", page.Name], pageId: page.Id);
            }

            foreach (var pool in page.Pools)
            {
                Id(pool.Id, S["question pool"], pageId: page.Id);
            }

            foreach (var question in page.Questions)
            {
                Id(question.Id, S["question"], question.Id, page.Id);

                var choiceIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (var choice in question.Choices)
                {
                    if (string.IsNullOrWhiteSpace(choice.Id) || !choiceIds.Add(choice.Id))
                    {
                        context.AddError("duplicate-choice-id", S["The question {0} has a choice without an id or with a repeated id.", Label(question)], question.Id, page.Id);
                    }
                }

                var rowIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (var row in question.Rows)
                {
                    if (string.IsNullOrWhiteSpace(row.Id) || !rowIds.Add(row.Id))
                    {
                        context.AddError("duplicate-row-id", S["The question {0} has a row without an id or with a repeated id.", Label(question)], question.Id, page.Id);
                    }
                }
            }
        }

        foreach (var ending in definition.Endings)
        {
            Id(ending.Id, S["ending"]);
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (page, _, question) in SurveyDefinitions.Questions(definition))
        {
            if (!SurveyNames.IsValidVariableCode(question.Name))
            {
                context.AddError("invalid-name", S["The question {0} needs a code of letters, digits and underscores that starts with a letter.", Label(question)], question.Id, page.Id);
            }
            else if (!names.Add(question.Name))
            {
                context.AddError("duplicate-name", S["The code {0} is used by more than one question or variable.", question.Name], question.Id, page.Id);
            }
        }

        foreach (var variable in definition.Variables)
        {
            if (!SurveyNames.IsValidVariableCode(variable.Name))
            {
                context.AddError("invalid-variable-name", S["The variable {0} needs a code of letters, digits and underscores that starts with a letter.", variable.Name ?? string.Empty]);
            }
            else if (!names.Add(variable.Name))
            {
                context.AddError("duplicate-name", S["The code {0} is used by more than one question or variable.", variable.Name]);
            }
        }
    }

    private void CheckQuestions(SurveyDefinitionValidationContext context, DefinitionMap map)
    {
        var answerable = 0;

        foreach (var (page, _, question) in SurveyDefinitions.Questions(context.Definition))
        {
            var descriptor = _registry.GetControl(question.ControlType);

            if (descriptor is null)
            {
                context.AddError("unknown-control", S["The question {0} uses the question type {1}, which is not available.", Label(question), question.ControlType ?? string.Empty], question.Id, page.Id);

                continue;
            }

            if (descriptor.Has(SurveyControlCapabilities.Answerable))
            {
                answerable++;
            }

            if (descriptor.Has(SurveyControlCapabilities.UsesChoices) && string.IsNullOrEmpty(question.ChoicesFrom) && !question.Choices.Any(choice => !choice.Retired))
            {
                context.AddError("no-choices", S["The question {0} needs at least one choice.", Label(question)], question.Id, page.Id);
            }

            if (!string.IsNullOrEmpty(question.ChoicesFrom))
            {
                if (!map.QuestionPage.TryGetValue(question.ChoicesFrom, out var sourcePage) || sourcePage >= map.QuestionPage[question.Id])
                {
                    context.AddError("invalid-carry-forward", S["The question {0} carries forward choices from a question that is not on an earlier page.", Label(question)], question.Id, page.Id);
                }
            }

            foreach (var rule in question.Validation)
            {
                if (rule.Kind == SurveyValidationKind.Regex && !IsValidRegex(rule.Value))
                {
                    context.AddError("invalid-regex", S["The pattern of the question {0} is not a valid regular expression.", Label(question)], question.Id, page.Id);
                }
            }
        }

        if (answerable == 0)
        {
            context.AddError("no-questions", S["The survey needs at least one question that collects an answer."]);
        }
    }

    private void CheckLogic(SurveyDefinitionValidationContext context, DefinitionMap map)
    {
        var definition = context.Definition;
        var visibilityRules = new Dictionary<string, ConditionRule>(StringComparer.Ordinal);

        for (var pageIndex = 0; pageIndex < definition.Pages.Count; pageIndex++)
        {
            var page = definition.Pages[pageIndex];

            if (HasRule(page.Visibility))
            {
                CheckRule(context, map, page.Visibility.Rule, pageIndex, allowSamePage: false, null, page.Id);
            }

            foreach (var question in page.Questions)
            {
                if (HasRule(question.Visibility))
                {
                    CheckRule(context, map, question.Visibility.Rule, pageIndex, allowSamePage: true, question.Id, page.Id, selfId: question.Id);
                    visibilityRules[question.Id] = question.Visibility.Rule;
                }

                if (!ConditionRule.IsEmpty(question.RequiredWhen))
                {
                    CheckRule(context, map, question.RequiredWhen, pageIndex, allowSamePage: true, question.Id, page.Id, selfId: question.Id);
                }

                if (!ConditionRule.IsEmpty(question.EnabledWhen))
                {
                    CheckRule(context, map, question.EnabledWhen, pageIndex, allowSamePage: true, question.Id, page.Id, selfId: question.Id);
                }

                foreach (var choice in question.Choices)
                {
                    if (HasRule(choice.Visibility))
                    {
                        CheckRule(context, map, choice.Visibility.Rule, pageIndex, allowSamePage: true, question.Id, page.Id, selfId: question.Id);
                    }
                }
            }
        }

        var cycle = ConditionalVisibility.FindCycle(visibilityRules);

        if (cycle.Count > 0)
        {
            var labels = cycle.Select(id => map.Questions.TryGetValue(id, out var question) ? Label(question) : id);
            context.AddError("logic-cycle", S["The visibility rules form a loop: {0}.", string.Join(" → ", labels)], cycle[0]);
        }
    }

    private void CheckBranchesAndEndings(SurveyDefinitionValidationContext context, DefinitionMap map)
    {
        var definition = context.Definition;
        var endingIds = definition.Endings.Select(ending => ending.Id).Where(id => id is not null).ToHashSet(StringComparer.Ordinal);

        for (var pageIndex = 0; pageIndex < definition.Pages.Count; pageIndex++)
        {
            var page = definition.Pages[pageIndex];

            foreach (var branch in page.Branches)
            {
                if (!ConditionRule.IsEmpty(branch.Rule))
                {
                    CheckRule(context, map, branch.Rule, pageIndex, allowSamePage: true, null, page.Id);
                }

                if (branch.TargetKind == BranchTargetKind.Page)
                {
                    if (!map.PageIndex.TryGetValue(branch.TargetId ?? string.Empty, out var target) || target <= pageIndex)
                    {
                        context.AddError("branch-backward", S["A branch of the page {0} must go to a later page.", PageLabel(page, pageIndex)], pageId: page.Id);
                    }
                }
                else if (!endingIds.Contains(branch.TargetId ?? string.Empty))
                {
                    context.AddError("branch-ending", S["A branch of the page {0} goes to an ending that does not exist.", PageLabel(page, pageIndex)], pageId: page.Id);
                }
            }
        }

        var defaults = definition.Endings.Count(ending => ending.IsDefault);

        if (defaults != 1)
        {
            context.AddError("default-ending", S["The survey needs exactly one default ending."]);
        }

        foreach (var ending in definition.Endings)
        {
            if (HasRule(ending.Condition))
            {
                CheckRule(context, map, ending.Condition.Rule, definition.Pages.Count, allowSamePage: true, null, null);
            }
        }

        foreach (var quota in definition.Quotas)
        {
            if (quota.Action == QuotaAction.ScreenOut && !endingIds.Contains(quota.EndingId ?? string.Empty))
            {
                context.AddError("quota-ending", S["The quota {0} screens out to an ending that does not exist.", quota.Title ?? quota.Name ?? string.Empty]);
            }
        }
    }

    private void CheckPools(SurveyDefinitionValidationContext context)
    {
        foreach (var page in context.Definition.Pages)
        {
            var poolIds = page.Pools.Select(pool => pool.Id).ToHashSet(StringComparer.Ordinal);

            foreach (var question in page.Questions.Where(question => !string.IsNullOrEmpty(question.PoolId)))
            {
                if (!poolIds.Contains(question.PoolId))
                {
                    context.AddError("unknown-pool", S["The question {0} belongs to a pool that is not on its page.", Label(question)], question.Id, page.Id);
                }
            }

            foreach (var pool in page.Pools)
            {
                var candidates = page.Questions.Count(question => question.PoolId == pool.Id);

                if (pool.DrawCount < 0)
                {
                    context.AddError("pool-draw", S["The pool {0} draws a negative number of questions.", pool.Title ?? pool.Name ?? string.Empty], pageId: page.Id);
                }
                else if (pool.DrawCount > candidates)
                {
                    context.AddError("pool-draw", S["The pool {0} draws {1} questions but has only {2}.", pool.Title ?? pool.Name ?? string.Empty, pool.DrawCount, candidates], pageId: page.Id);
                }
            }
        }
    }

    private void CheckScoring(SurveyDefinitionValidationContext context, DefinitionMap map)
    {
        var definition = context.Definition;
        var categories = definition.ScoreCategories.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var (page, _, question) in SurveyDefinitions.Questions(definition))
        {
            var scoring = question.Scoring ?? new SurveyQuestionScoring();

            if (!string.IsNullOrEmpty(scoring.CategoryId) && !categories.Contains(scoring.CategoryId))
            {
                context.AddError("unknown-category", S["The question {0} adds to a score category that does not exist.", Label(question)], question.Id, page.Id);
            }

            if (!definition.Scoring.Enabled)
            {
                continue;
            }

            var descriptor = _registry.GetControl(question.ControlType);

            if (descriptor is null || !descriptor.Has(SurveyControlCapabilities.Scorable) || descriptor.Has(SurveyControlCapabilities.ManuallyGraded))
            {
                continue;
            }

            var points = scoring.Points ?? definition.Scoring.DefaultPoints;

            if (points == 0)
            {
                continue;
            }

            var hasKey = scoring.CorrectChoiceIds.Count > 0
                || scoring.ChoicePoints.Count > 0
                || scoring.AcceptedAnswers.Count > 0
                || scoring.NumberAnswer is not null
                || scoring.Pairs.Count > 0
                || scoring.Key.Count > 0;

            if (!hasKey)
            {
                context.AddWarning("no-key", S["The question {0} gives points but has no right answer.", Label(question)], question.Id, page.Id);
            }

            foreach (var choiceId in scoring.CorrectChoiceIds.Concat(scoring.ChoicePoints.Keys))
            {
                if (!question.Choices.Any(choice => choice.Id == choiceId) && string.IsNullOrEmpty(question.ChoicesFrom))
                {
                    context.AddError("unknown-key-choice", S["The right answer of the question {0} names a choice that does not exist.", Label(question)], question.Id, page.Id);

                    break;
                }
            }
        }

        if (definition.Scoring.PassMarkPercent is { } passMark && (passMark < 0 || passMark > 100))
        {
            context.AddError("pass-mark", S["The pass mark must be between 0 and 100."]);
        }

        // A respondent who can change a finished response must not see the key first: they could read the right
        // answers, reopen and submit again for a perfect score. After the survey closes no response can be reopened.
        const ReviewShows key = ReviewShows.RightAnswers | ReviewShows.Feedback;
        var review = definition.Review ?? new SurveyReviewSettings();

        if (definition.Scoring.Enabled
            && definition.Navigation?.AllowEditAfterSubmit == true
            && ((review.ImmediatelyAfter | review.LaterWhileOpen) & key) != 0)
        {
            context.AddError("edit-after-review", S["Respondents can't change a finished response while the review shows the right answers or feedback before the survey closes. Turn off one of the two."]);
        }
    }

    private void CheckPiping(SurveyDefinitionValidationContext context, DefinitionMap map)
    {
        var definition = context.Definition;

        for (var pageIndex = 0; pageIndex < definition.Pages.Count; pageIndex++)
        {
            var page = definition.Pages[pageIndex];

            CheckPipes(context, map, page.Title, pageIndex, -1, null, page.Id);
            CheckPipes(context, map, page.Description, pageIndex, -1, null, page.Id);

            for (var position = 0; position < page.Questions.Count; position++)
            {
                var question = page.Questions[position];

                CheckPipes(context, map, question.Title, pageIndex, position, question.Id, page.Id);
                CheckPipes(context, map, question.Description, pageIndex, position, question.Id, page.Id);

                foreach (var choice in question.Choices)
                {
                    CheckPipes(context, map, choice.Text, pageIndex, position, question.Id, page.Id);
                }
            }
        }

        foreach (var ending in definition.Endings)
        {
            CheckPipes(context, map, ending.Message, definition.Pages.Count, int.MaxValue, null, null);
            CheckPipes(context, map, ending.RedirectUrl, definition.Pages.Count, int.MaxValue, null, null);
        }
    }

    private void CheckFormulas(SurveyDefinitionValidationContext context)
    {
        var definition = context.Definition;

        void Formula(string formula, string questionId, string pageId)
        {
            if (!string.IsNullOrWhiteSpace(formula) && !_formulaValidator.TryCompile(definition, formula, out var error))
            {
                context.AddError("invalid-formula", S["The formula {0} is not valid: {1}", formula, error ?? string.Empty], questionId, pageId);
            }
        }

        foreach (var page in definition.Pages)
        {
            Formula(page.Visibility?.Formula, null, page.Id);

            foreach (var branch in page.Branches)
            {
                Formula(branch.Formula, null, page.Id);
            }

            foreach (var question in page.Questions)
            {
                Formula(question.Visibility?.Formula, question.Id, page.Id);

                foreach (var rule in question.Validation.Where(rule => rule.Kind == SurveyValidationKind.Formula))
                {
                    Formula(rule.Value, question.Id, page.Id);
                }
            }
        }

        foreach (var variable in definition.Variables.Where(variable => variable.Kind == VariableKind.Calculated))
        {
            if (string.IsNullOrWhiteSpace(variable.Formula))
            {
                context.AddError("missing-formula", S["The calculated variable {0} needs a formula.", variable.Name ?? string.Empty]);
            }
            else
            {
                Formula(variable.Formula, null, null);
            }
        }

        foreach (var ending in definition.Endings)
        {
            Formula(ending.Condition?.Formula, null, null);
        }

        Formula(definition.Scoring.ScoreFormula, null, null);
    }

    private void CheckRule(
        SurveyDefinitionValidationContext context,
        DefinitionMap map,
        ConditionRule rule,
        int pageIndex,
        bool allowSamePage,
        string questionId,
        string pageId,
        string selfId = null)
    {
        foreach (var source in ConditionalVisibility.Sources(rule))
        {
            if (!IsKnownSource(context.Definition, map, source, pageIndex, allowSamePage, selfId, out var problem))
            {
                context.AddError("invalid-rule-source", problem, questionId, pageId);
            }
        }

        foreach (var condition in AllConditions(rule).Where(condition => condition is not null && !string.IsNullOrWhiteSpace(condition.Source)))
        {
            if (condition.Operator is not (ConditionOperator.Is or ConditionOperator.IsNot or ConditionOperator.IsOneOf or ConditionOperator.IsNoneOf))
            {
                continue;
            }

            var baseId = condition.Source.Split('.')[0];

            if (!map.Questions.TryGetValue(baseId, out var source) || source.Choices.Count == 0 || !string.IsNullOrEmpty(source.ChoicesFrom))
            {
                continue;
            }

            foreach (var value in condition.Values ?? [])
            {
                if (!source.Choices.Any(choice => choice.Id == value || string.Equals(choice.Value, value, StringComparison.OrdinalIgnoreCase)))
                {
                    context.AddError("unknown-rule-choice", S["A rule compares the question {0} with a choice that does not exist.", Label(source)], questionId, pageId);

                    break;
                }
            }
        }
    }

    private bool IsKnownSource(SurveyDefinition definition, DefinitionMap map, string source, int pageIndex, bool allowSamePage, string selfId, out string problem)
    {
        problem = null;

        if (source.StartsWith("var:", StringComparison.Ordinal))
        {
            if (definition.Variables.Any(variable => string.Equals(variable.Name, source.Substring(4), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            problem = S["A rule reads the variable {0}, which does not exist.", source.Substring(4)];

            return false;
        }

        if (source == "score" || source == "collector" || source == "culture" || source.StartsWith("context:", StringComparison.Ordinal))
        {
            return true;
        }

        if (source.StartsWith("score:", StringComparison.Ordinal))
        {
            var category = source.Substring(6);

            if (definition.ScoreCategories.Any(item => item.Id == category || string.Equals(item.Name, category, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            problem = S["A rule reads the score category {0}, which does not exist.", category];

            return false;
        }

        var parts = source.Split('.', 2);

        if (!map.Questions.TryGetValue(parts[0], out var question))
        {
            problem = S["A rule reads the question {0}, which does not exist.", source];

            return false;
        }

        if (parts.Length == 2 && !question.Rows.Any(row => row.Id == parts[1]))
        {
            problem = S["A rule reads a row of the question {0} that does not exist.", Label(question)];

            return false;
        }

        if (question.Id == selfId)
        {
            problem = S["The question {0} has a rule that reads its own answer.", Label(question)];

            return false;
        }

        var sourcePage = map.QuestionPage[question.Id];

        if (sourcePage > pageIndex || (!allowSamePage && sourcePage == pageIndex))
        {
            problem = S["A rule reads the question {0}, which comes later in the survey.", Label(question)];

            return false;
        }

        return true;
    }

    private void CheckPipes(SurveyDefinitionValidationContext context, DefinitionMap map, string text, int pageIndex, int position, string questionId, string pageId)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('{'))
        {
            return;
        }

        foreach (Match match in _piping.Matches(text))
        {
            var name = match.Groups[1].Value;

            if (name is "score" or "percent" || name.StartsWith("score:", StringComparison.Ordinal))
            {
                continue;
            }

            if (name.StartsWith("var:", StringComparison.Ordinal))
            {
                if (!context.Definition.Variables.Any(variable => string.Equals(variable.Name, name.Substring(4), StringComparison.OrdinalIgnoreCase)))
                {
                    context.AddError("unknown-pipe", S["The text {{{0}}} names a variable that does not exist.", name], questionId, pageId);
                }

                continue;
            }

            var baseName = name.Split('.')[0];

            if (context.Definition.Variables.Any(variable => string.Equals(variable.Name, baseName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!map.QuestionsByName.TryGetValue(baseName, out var source))
            {
                context.AddError("unknown-pipe", S["The text {{{0}}} names a question that does not exist.", name], questionId, pageId);

                continue;
            }

            var sourcePage = map.QuestionPage[source.Id];
            var sourcePosition = map.QuestionPosition[source.Id];

            if (sourcePage > pageIndex || (sourcePage == pageIndex && sourcePosition >= position))
            {
                context.AddError("late-pipe", S["The text {{{0}}} names a question that comes later in the survey.", name], questionId, pageId);
            }
        }
    }

    private static bool HasRule(SurveyVisibility visibility)
        => visibility is not null && visibility.Mode != VisibilityMode.Always && !ConditionRule.IsEmpty(visibility.Rule);

    private static bool IsValidRegex(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.None, _regexTimeout);

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string Label(SurveyQuestion question)
        => string.IsNullOrWhiteSpace(question.Name) ? question.Title ?? question.Id ?? string.Empty : question.Name;

    private static string PageLabel(SurveyPage page, int index)
        => string.IsNullOrWhiteSpace(page.Title) ? (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : page.Title;

    private sealed class DefinitionMap
    {
        public DefinitionMap(SurveyDefinition definition)
        {
            for (var i = 0; i < definition.Pages.Count; i++)
            {
                var page = definition.Pages[i];

                if (!string.IsNullOrEmpty(page.Id))
                {
                    PageIndex.TryAdd(page.Id, i);
                }

                for (var position = 0; position < page.Questions.Count; position++)
                {
                    var question = page.Questions[position];

                    if (string.IsNullOrEmpty(question.Id))
                    {
                        continue;
                    }

                    Questions.TryAdd(question.Id, question);
                    QuestionPage.TryAdd(question.Id, i);
                    QuestionPosition.TryAdd(question.Id, position);

                    if (!string.IsNullOrEmpty(question.Name))
                    {
                        QuestionsByName.TryAdd(question.Name, question);
                    }
                }
            }
        }

        public Dictionary<string, int> PageIndex { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, SurveyQuestion> Questions { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, SurveyQuestion> QuestionsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, int> QuestionPage { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, int> QuestionPosition { get; } = new(StringComparer.Ordinal);
    }

    private static IEnumerable<Condition> AllConditions(ConditionRule rule)
    {
        if (rule is null)
        {
            yield break;
        }

        foreach (var condition in rule.Conditions ?? [])
        {
            yield return condition;
        }

        foreach (var group in rule.Groups ?? [])
        {
            foreach (var condition in AllConditions(group))
            {
                yield return condition;
            }
        }
    }
}
