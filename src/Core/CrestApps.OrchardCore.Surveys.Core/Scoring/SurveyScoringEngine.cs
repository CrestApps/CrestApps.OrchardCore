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
    public Task PrepareAsync()
        => _hiddenQuestionResolver.PrepareAsync();

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
            // The formula reads this response: the question scores (SCORE), the category scores by id ([score:name]),
            // and, as its score and percent, the sum of the points and its percent before the formula ([score],
            // [percent]), unrounded.
            response.Answers ??= [];
            response.Score = total;
            response.Percent = max > 0 ? total / max * 100m : null;

            if (_formulaEvaluator.Evaluate(definition, response) is { } formulaTotal)
            {
                total = formulaTotal;
            }
        }

        // The pass and the band are decided on the exact percent: 69.5% doesn't pass a 70% mark because it shows as 70%.
        var percent = max > 0 ? total / max * 100m : (decimal?)null;

        response.Score = Math.Round(total, decimals, MidpointRounding.AwayFromZero);
        response.MaxScore = Math.Round(max, decimals, MidpointRounding.AwayFromZero);
        response.Percent = percent is { } exact ? Math.Round(exact, decimals, MidpointRounding.AwayFromZero) : null;
        response.Grading = pending ? GradingStatus.Pending : GradingStatus.Graded;

        ApplyResult(definition, response, percent);
    }

    /// <summary>
    /// Sets whether a response passed and its score band from its percent. Nothing passes while grading is pending.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    public static void ApplyResult(SurveyDefinition definition, SurveyResponse response)
        => ApplyResult(definition, response, response?.Percent);

    /// <summary>
    /// Sets whether a response passed and its score band from its exact (unrounded) percent, so a percent that only
    /// rounds up to the pass mark or a band's minimum doesn't reach it. Nothing passes while grading is pending.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response, whose <see cref="SurveyResponse.Percent"/> is the rounded percent.</param>
    /// <param name="exactPercent">The unrounded percent, or <see langword="null"/> when there is none.</param>
    public static void ApplyResult(SurveyDefinition definition, SurveyResponse response, decimal? exactPercent)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        var band = SurveyPassRule.FindBand(definition, exactPercent);

        response.ScoreBandId = band?.Id;

        if (response.Grading != GradingStatus.Graded || exactPercent is null)
        {
            response.Passed = null;

            return;
        }

        response.Passed = SurveyPassRule.Decide(definition, exactPercent);
    }

    /// <summary>
    /// Lists the questions that count toward a response's score, in the order the respondent saw them: the questions of
    /// the pages on the respondent's path that logic did not hide, then every question whose answer is locked (checked
    /// with immediate feedback) that a later branch or visibility rule took off the path or hid. The respondent's own
    /// changes can't do that (the response service refuses them), so such an answer comes only from the author's logic,
    /// such as a rule on the time. The respondent saw a locked answer graded and it is kept, so it counts with its points
    /// and its most points alike. The completion scores a response as completed, so a completion, a regrade and the
    /// review page count the same questions (see <see cref="ScreensOnPath"/>); the grading page shows them too.
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

    /// <summary>
    /// Lists the screens on a finished response's path, in order: the screens the respondent reached, then the screens
    /// still ahead of them when the response was submitted for them at its deadline or once its survey closed. The
    /// unreached screens count toward the score with 0 points (the respondent left them unanswered), so walking away from
    /// a timed or closing quiz can't pass it on the pages answered so far; the screens a branch skipped stay out. The
    /// completion, a regrade and the review page all read this list.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The screen ids.</returns>
    public static IEnumerable<string> ScreensOnPath(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return (response.PagePath ?? [])
            .Concat(response.UnreachedPagePath ?? [])
            .Where(screen => !string.IsNullOrEmpty(screen))
            .Distinct(StringComparer.Ordinal);
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
        var pages = response.PagePath is { Count: > 0 } && response.Status != SurveyResponseStatus.InProgress
            ? ScreensOnPath(response).Where(order.ContainsKey)
            : order.Keys;

        return pages
            .SelectMany(pageId => order[pageId] ?? [])
            .Distinct(StringComparer.Ordinal)
            .Select(id => byId.GetValueOrDefault(id))
            .Where(question => question is not null);
    }
}
