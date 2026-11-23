using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Scoring;

/// <summary>
/// Scores a response with a definition's key: every question the respondent was shown, through its type's scorer.
/// Unanswered questions score 0, hidden ones are left out of the most points (except a locked answer the respondent
/// already saw graded, which still counts), and grader scores are kept.
/// </summary>
public sealed class SurveyScoringEngine : ISurveyScoringEngine
{
    private readonly ISurveyControlRegistry _registry;
    private readonly ISurveyScoreFormulaEvaluator _formulaEvaluator;
    private readonly ISurveyHiddenQuestionResolver _hiddenQuestionResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyScoringEngine"/> class.
    /// </summary>
    /// <param name="registry">The question types.</param>
    /// <param name="formulaEvaluator">The score formula evaluator.</param>
    /// <param name="hiddenQuestionResolver">Tells which questions logic hid.</param>
    public SurveyScoringEngine(
        ISurveyControlRegistry registry,
        ISurveyScoreFormulaEvaluator formulaEvaluator,
        ISurveyHiddenQuestionResolver hiddenQuestionResolver)
    {
        _registry = registry;
        _formulaEvaluator = formulaEvaluator;
        _hiddenQuestionResolver = hiddenQuestionResolver;
    }

    /// <inheritdoc />
    public void Score(SurveyDefinition definition, SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        var settings = definition.Scoring ?? new SurveyScoringSettings();

        response.CategoryScores ??= [];
        response.CategoryScores.Clear();

        if (!settings.Enabled)
        {
            response.Grading = GradingStatus.NotScored;
            response.Score = null;
            response.MaxScore = null;
            response.Percent = null;
            response.Passed = null;
            response.ScoreBandId = null;

            return;
        }

        var decimals = Math.Clamp(settings.RoundingDecimals, 0, 6);
        var hidden = _hiddenQuestionResolver.GetHiddenQuestionIds(definition, response);
        var total = 0m;
        var max = 0m;
        var pending = false;

        foreach (var question in CountedQuestions(definition, response, hidden))
        {
            if (_registry.GetScorer(question.ControlType) is not { } scorer)
            {
                continue;
            }

            var context = new SurveyScoringContext
            {
                Definition = definition,
                Settings = settings,
                ShownChoiceIds = response.Plan?.ChoiceOrder is { } order && order.TryGetValue(question.Id, out var choices) ? choices : [],
            };

            var questionMax = scorer.MaxPoints(question, context);

            if (questionMax <= 0)
            {
                continue;
            }

            response.Answers ??= [];
            response.Answers.TryGetValue(question.Id, out var answer);

            var empty = answer is null || (_registry.GetHandler(question.ControlType) is { } handler && handler.IsEmpty(question, answer));
            decimal points;

            if (empty)
            {
                // An unanswered question scores 0 and needs no grader.
                points = answer?.ManualScore ?? 0;

                if (answer is not null)
                {
                    answer.AutoScore = 0;
                    answer.MaxPoints = questionMax;
                }
            }
            else
            {
                var result = scorer.Score(question, answer, context);

                answer.AutoScore = result?.Points is { } auto ? Math.Round(auto, decimals, MidpointRounding.AwayFromZero) : null;
                answer.MaxPoints = questionMax;

                if (answer.ManualScore is { } manual)
                {
                    points = manual;
                }
                else if (answer.AutoScore is { } scored)
                {
                    points = scored;
                }
                else
                {
                    pending = true;
                    points = 0;
                }
            }

            total += points;
            max += questionMax;

            if (!string.IsNullOrEmpty(question.Scoring?.CategoryId))
            {
                response.CategoryScores[question.Scoring.CategoryId] = response.CategoryScores.GetValueOrDefault(question.Scoring.CategoryId) + points;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.ScoreFormula))
        {
            var byName = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var category in definition.ScoreCategories ?? [])
            {
                if (!string.IsNullOrEmpty(category?.Name))
                {
                    byName[category.Name] = response.CategoryScores.GetValueOrDefault(category.Id);
                }
            }

            if (_formulaEvaluator.Evaluate(definition, byName, total) is { } formulaTotal)
            {
                total = formulaTotal;
            }
        }

        response.Score = Math.Round(total, decimals, MidpointRounding.AwayFromZero);
        response.MaxScore = Math.Round(max, decimals, MidpointRounding.AwayFromZero);
        response.Percent = max > 0
            ? Math.Round(total / max * 100m, decimals, MidpointRounding.AwayFromZero)
            : null;
        response.Grading = pending ? GradingStatus.Pending : GradingStatus.Graded;

        ApplyResult(definition, response);
    }

    /// <summary>
    /// Sets whether a response passed and its score band from its percent. Nothing passes while grading is pending.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    public static void ApplyResult(SurveyDefinition definition, SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        var settings = definition.Scoring ?? new SurveyScoringSettings();
        var band = response.Percent is { } percent
            ? (definition.ScoreBands ?? [])
                .Where(candidate => candidate is not null && candidate.MinPercent <= percent)
                .OrderByDescending(candidate => candidate.MinPercent)
                .FirstOrDefault()
            : null;

        response.ScoreBandId = band?.Id;

        if (response.Grading != GradingStatus.Graded || response.Percent is null)
        {
            response.Passed = null;

            return;
        }

        response.Passed = band?.Passing ?? (settings.PassMarkPercent is { } mark ? response.Percent >= mark : null);
    }

    /// <summary>
    /// Lists the questions that count toward a response's score, in the order the respondent saw them: the questions of
    /// the pages on the respondent's path that logic did not hide, then every question whose answer is locked (checked
    /// with immediate feedback) that a later branch or visibility rule took off the path or hid. The respondent saw a
    /// locked answer graded and it is kept, so it counts with its points and its most points alike. The review and the
    /// grading page show the same questions.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="hidden">The ids of the questions logic hid.</param>
    /// <returns>The questions.</returns>
    public static IEnumerable<SurveyQuestion> CountedQuestions(SurveyDefinition definition, SurveyResponse response, IReadOnlySet<string> hidden)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        var byId = new Dictionary<string, SurveyQuestion>(StringComparer.Ordinal);

        foreach (var question in (definition.Pages ?? []).SelectMany(page => page?.Questions ?? []))
        {
            if (!string.IsNullOrEmpty(question?.Id))
            {
                byId.TryAdd(question.Id, question);
            }
        }

        var counted = new List<SurveyQuestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var question in ShownQuestions(byId, response))
        {
            if (seen.Add(question.Id) && (hidden is null || !hidden.Contains(question.Id) || IsLocked(response, question.Id)))
            {
                counted.Add(question);
            }
        }

        foreach (var question in byId.Values)
        {
            if (!seen.Contains(question.Id) && IsLocked(response, question.Id))
            {
                seen.Add(question.Id);
                counted.Add(question);
            }
        }

        return counted;
    }

    private static bool IsLocked(SurveyResponse response, string questionId)
        => response.Answers is not null && response.Answers.TryGetValue(questionId, out var answer) && answer?.IsLocked == true;

    private static IEnumerable<SurveyQuestion> ShownQuestions(Dictionary<string, SurveyQuestion> byId, SurveyResponse response)
    {
        var order = response.Plan?.QuestionOrder;

        if (order is null || order.Count == 0)
        {
            // A response without a plan (a test, an import) was shown every question.
            return byId.Values;
        }

        // Pages skipped by a branch were never shown; when the response kept no path every planned page counts.
        var pages = response.PagePath is { Count: > 0 } path && response.Status != SurveyResponseStatus.InProgress
            ? path.Where(order.ContainsKey).Distinct(StringComparer.Ordinal)
            : order.Keys;

        return pages
            .SelectMany(pageId => order[pageId] ?? [])
            .Distinct(StringComparer.Ordinal)
            .Select(id => byId.GetValueOrDefault(id))
            .Where(question => question is not null);
    }
}
