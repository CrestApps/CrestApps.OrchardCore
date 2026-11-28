using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Formulas;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Translations;
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
    private static readonly Regex _liquidMarkup = new(@"\{\{.*?\}\}|\{%.*?%\}", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex _liquidScore = new(@"(?<![.\w""'])Score\b|\bResponse\s*(?:\.\s*|\[\s*['""])(?:Percent|Passed)\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex _formulaScore = new(@"\[\s*(?:score|percent)\s*(?:[:\]])|\bSCORE\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex _formulaVariable = new(@"\[\s*var\s*:\s*([^\]]+?)\s*\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

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
        var map = new DefinitionMap(definition)
        {
            FormulaScope = new SurveyFormulaScope(definition, _registry),
        };

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

    /// <summary>
    /// Gets whether a definition has a matrix row id longer than the answer index keeps, which would merge with another
    /// row that shares its first characters. Publishing refuses it; an import of a published version checks it here.
    /// </summary>
    /// <param name="definition">The definition, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a row id is too long.</returns>
    public static bool HasTooLongRowId(SurveyDefinition definition)
        => definition is not null
            && SurveyDefinitions.Questions(definition).Any(entry => entry.Question.Rows.Any(row => row.Id?.Length > SurveyAnswerIndex.KeyLength));

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
                    else if (row.Id.Length > SurveyAnswerIndex.KeyLength)
                    {
                        // The answer index keeps the first characters of a row id only, so two longer ids that share them
                        // would merge into one in results and filters.
                        context.AddError("long-row-id", S["The question {0} has a row id longer than {1} characters.", Label(question), SurveyAnswerIndex.KeyLength], question.Id, page.Id);
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

            if (HasFormula(page.Visibility))
            {
                CheckFormulaSources(context, map, page.Visibility.Formula, pageIndex, allowSamePage: false, null, page.Id);
            }

            foreach (var question in page.Questions)
            {
                if (HasRule(question.Visibility))
                {
                    CheckRule(context, map, question.Visibility.Rule, pageIndex, allowSamePage: true, question.Id, page.Id, selfId: question.Id);
                    visibilityRules[question.Id] = question.Visibility.Rule;
                }

                if (HasFormula(question.Visibility))
                {
                    CheckFormulaSources(context, map, question.Visibility.Formula, pageIndex, allowSamePage: true, question.Id, page.Id, selfId: question.Id);
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

                    if (HasFormula(choice.Visibility))
                    {
                        CheckFormulaSources(context, map, choice.Visibility.Formula, pageIndex, allowSamePage: true, question.Id, page.Id, selfId: question.Id);
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

                CheckFormulaSources(context, map, branch.Formula, pageIndex, allowSamePage: true, null, page.Id);

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

        // The runner keeps the designed page order when a page branches to another page (a shuffle would skip pages the
        // author never meant to skip), so the setting would silently do nothing.
        if (definition.Presentation?.PageOrder == QuestionOrder.Shuffle
            && definition.Pages.Count(page => !string.IsNullOrEmpty(page?.Id)) > 3
            && SurveyPlanBuilder.KeepsPageOrder(definition))
        {
            context.AddWarning("shuffle-with-branches", S["The pages are set to shuffle, but a page branches to another page, so respondents see the pages in the designed order."]);
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

            // An ending's message is restricted Liquid, as the respondent sees it rendered: the author learns here, not
            // from a message shown as written.
            // Translations of the message render the same way.
            var messages = definition.Translations?.Values
                .Select(texts => texts.TryGetValue(CrestApps.OrchardCore.Surveys.Core.Translations.SurveyTextKeys.EndingMessage(ending.Id), out var text) ? text : null)
                .Prepend(ending.Message) ?? [ending.Message];

            foreach (var message in messages)
            {
                if (!CrestApps.OrchardCore.Core.Liquid.RestrictedLiquid.TryValidate(message, out var liquidError))
                {
                    context.AddError("ending-liquid", S["The message of the ending {0} isn't valid Liquid for an ending: {1}", ending.Title ?? ending.Name ?? string.Empty, liquidError ?? string.Empty]);

                    break;
                }
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
        // answers, reopen and submit again for a perfect score. The score gives the key away too, one answer at a time:
        // reopens are unlimited, so changing one answer and resubmitting tells whether it was right. After the survey
        // closes no response can be reopened.
        var key = ReviewShows.RightAnswers | ReviewShows.Feedback;

        if (definition.Scoring.ShowScoreToRespondent)
        {
            key |= ReviewShows.Score;
        }

        var review = definition.Review ?? new SurveyReviewSettings();

        if (definition.Scoring.Enabled
            && definition.Navigation?.AllowEditAfterSubmit == true
            && ((review.ImmediatelyAfter | review.LaterWhileOpen) & key) != 0)
        {
            context.AddError("edit-after-review", S["Respondents can't change a finished response while the review shows the score, the right answers or feedback before the survey closes. Turn off one of the two."]);
        }

        if (definition.Scoring.Enabled && definition.Navigation?.AllowEditAfterSubmit == true && EndingReadsTheScore(definition))
        {
            // An ending that shows the score, or is chosen by it ("Passed" / "Try again"), gives the key away the same
            // way: submit, read the ending, reopen, change one answer and submit again.
            context.AddError("edit-after-score-ending", S["Respondents can change a finished response, so no ending may show the score, the percent or whether the response passed, or be chosen by the score. Turn off changing finished responses, or take the score out of the endings."]);
        }
    }

    private static bool EndingReadsTheScore(SurveyDefinition definition)
    {
        var scoreVariables = ScoreVariables(definition);

        bool TextReads(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (_liquidMarkup.Matches(text).Any(match => _liquidScore.IsMatch(match.Value)))
            {
                return true;
            }

            return SurveyPipingRenderer.References(text).Any(name => SurveyPipingRenderer.IsScoreReference(name)
                || (name.StartsWith(SurveyConditionSources.VariablePrefix, StringComparison.Ordinal) && scoreVariables.Contains(name.Substring(SurveyConditionSources.VariablePrefix.Length))));
        }

        bool RuleReads(ConditionRule rule)
            => rule is not null
                && ((rule.Conditions ?? []).Any(condition => IsScoreSource(condition?.Source, scoreVariables))
                    || (rule.Groups ?? []).Any(RuleReads));

        var endings = (definition.Endings ?? []).Where(ending => ending is not null).ToList();

        if (endings.Any(ending => TextReads(ending.Message) || TextReads(ending.RedirectUrl)) || TextReads(definition.Presentation?.CompletedRedirectUrl))
        {
            return true;
        }

        var messageKeys = endings.Where(ending => !string.IsNullOrEmpty(ending.Id)).Select(ending => SurveyTextKeys.EndingMessage(ending.Id)).ToHashSet(StringComparer.Ordinal);

        if ((definition.Translations ?? []).Values.Any(texts => texts is not null && texts.Any(text => messageKeys.Contains(text.Key) && TextReads(text.Value))))
        {
            return true;
        }

        return endings.Any(ending => !ending.IsDefault
            && ending.Condition is { } condition
            && condition.Mode != VisibilityMode.Always
            && (RuleReads(condition.Rule) || FormulaReads(condition.Formula, scoreVariables)));
    }

    private static bool IsScoreSource(string source, HashSet<string> scoreVariables)
        => !string.IsNullOrEmpty(source)
            && (string.Equals(source, SurveyConditionSources.Score, StringComparison.Ordinal)
                || source.StartsWith(SurveyConditionSources.ScorePrefix, StringComparison.Ordinal)
                || (source.StartsWith(SurveyConditionSources.VariablePrefix, StringComparison.Ordinal) && scoreVariables.Contains(source.Substring(SurveyConditionSources.VariablePrefix.Length))));

    private static bool FormulaReads(string formula, HashSet<string> scoreVariables)
        => !string.IsNullOrWhiteSpace(formula)
            && (_formulaScore.IsMatch(formula) || _formulaVariable.Matches(formula).Any(match => scoreVariables.Contains(match.Groups[1].Value)));

    private static HashSet<string> ScoreVariables(SurveyDefinition definition)
    {
        // The calculated variables whose formula reads the score, directly or through another such variable.
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var calculated = (definition.Variables ?? [])
            .Where(variable => variable is not null && variable.Kind == VariableKind.Calculated && !string.IsNullOrEmpty(variable.Name))
            .ToList();

        bool added;

        do
        {
            added = false;

            foreach (var variable in calculated)
            {
                if (!variables.Contains(variable.Name) && FormulaReads(variable.Formula, variables))
                {
                    variables.Add(variable.Name);
                    added = true;
                }
            }
        }
        while (added);

        return variables;
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
            var variable = definition.Variables.FirstOrDefault(variable => string.Equals(variable.Name, source.Substring(4), StringComparison.OrdinalIgnoreCase));

            if (variable is null)
            {
                problem = S["A rule reads the variable {0}, which does not exist.", source.Substring(4)];

                return false;
            }

            // A calculated variable is as late as the latest question its formula reads.
            if (variable.Kind == VariableKind.Calculated && LateQuestionRead(definition, map, variable.Formula, pageIndex, allowSamePage, selfId) is { } lateRead)
            {
                problem = S["A rule reads the variable {0}, whose formula reads the question {1}, which comes later in the survey or is the question itself.", variable.Name, Label(lateRead)];

                return false;
            }

            return true;
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

        // A calculated question is as late as the latest question its formula reads.
        if (CalculatedFormula(question) is { } formula && LateQuestionRead(definition, map, formula, pageIndex, allowSamePage, selfId) is { } late)
        {
            problem = S["A rule reads the question {0}, whose formula reads the question {1}, which comes later in the survey or is the question itself.", Label(question), Label(late)];

            return false;
        }

        return true;
    }

    // Checks a formula that decides the respondent's path or what they see (a page's or question's visibility, a branch)
    // as a rule is checked: it may read only questions on earlier pages (on the same page too when allowed), never the
    // question it belongs to, through every calculated variable and calculated question it reads. A formula that reads a
    // later question could let an answer given later change whether an earlier page counted.
    private void CheckFormulaSources(SurveyDefinitionValidationContext context, DefinitionMap map, string formula, int pageIndex, bool allowSamePage, string questionId, string pageId, string selfId = null)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return;
        }

        if (LateQuestionRead(context.Definition, map, formula, pageIndex, allowSamePage, selfId) is { } late)
        {
            context.AddError(
                "invalid-formula-source",
                late.Id == selfId
                    ? S["The question {0} has a formula that reads its own answer.", Label(late)]
                    : S["A formula reads the question {0}, which comes later in the survey.", Label(late)],
                questionId,
                pageId);
        }
    }

    // The first question a formula reads (through the calculated variables and calculated questions it reads) that comes
    // after the page it is read on (or on that page, unless allowed), or is the question the formula belongs to; null when
    // there is none.
    private SurveyQuestion LateQuestionRead(SurveyDefinition definition, DefinitionMap map, string formula, int pageIndex, bool allowSamePage, string selfId)
    {
        foreach (var questionId in QuestionsReadBy(definition, map, formula))
        {
            if (!map.Questions.TryGetValue(questionId, out var question) || !map.QuestionPage.TryGetValue(questionId, out var sourcePage))
            {
                continue;
            }

            if (questionId == selfId || sourcePage > pageIndex || (!allowSamePage && sourcePage == pageIndex))
            {
                return question;
            }
        }

        return null;
    }

    // The ids of the questions a formula reads, with those read by the calculated variables and calculated questions it
    // reads, and theirs in turn.
    private HashSet<string> QuestionsReadBy(SurveyDefinition definition, DefinitionMap map, string formula)
    {
        var questions = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();

        if (map.FormulaScope is null || string.IsNullOrWhiteSpace(formula))
        {
            return questions;
        }

        pending.Push(formula);

        while (pending.Count > 0)
        {
            foreach (var reference in _formulaValidator.GetReferences(definition, pending.Pop()))
            {
                var field = map.FormulaScope.Find(reference);

                if (field is null || !visited.Add(field.Key))
                {
                    continue;
                }

                if (field.Kind == SurveyFormulaFieldKind.Question)
                {
                    questions.Add(field.Id);

                    if (map.Questions.TryGetValue(field.Id, out var question) && CalculatedFormula(question) is { } calculated)
                    {
                        pending.Push(calculated);
                    }
                }
                else if (field.Kind == SurveyFormulaFieldKind.Variable
                    && definition.Variables.FirstOrDefault(variable => string.Equals(variable?.Name, field.Id, StringComparison.OrdinalIgnoreCase)) is { Kind: VariableKind.Calculated, Formula: { Length: > 0 } variableFormula })
                {
                    pending.Push(variableFormula);
                }
            }
        }

        return questions;
    }

    // The formula of a calculated question, or null.
    private string CalculatedFormula(SurveyQuestion question)
        => question is not null && _registry.GetHandler(question.ControlType) is ISurveyCalculatedControl control && control.GetFormula(question) is { Length: > 0 } formula
            ? formula
            : null;

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

    private static bool HasFormula(SurveyVisibility visibility)
        => visibility is not null && visibility.Mode != VisibilityMode.Always && !string.IsNullOrWhiteSpace(visibility.Formula);

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

        public SurveyFormulaScope FormulaScope { get; init; }
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
