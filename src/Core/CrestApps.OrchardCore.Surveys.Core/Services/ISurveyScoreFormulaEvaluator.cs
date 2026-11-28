using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Evaluates a survey's score formula for a response being scored. The default returns <see langword="null"/>, so the
/// total is the sum of the question points; the formula services replace it.
/// </summary>
public interface ISurveyScoreFormulaEvaluator
{
    /// <summary>
    /// Evaluates the score formula of a definition for a response the scoring engine is scoring. The response carries
    /// what the formula reads: the question scores (<see cref="SurveyAnswer.Score"/>, for <c>SCORE('name')</c>), the
    /// category scores by category id (for <c>[score:category]</c>), and, as its <see cref="SurveyResponse.Score"/> and
    /// <see cref="SurveyResponse.Percent"/>, the sum of the question points and that sum's percent of the most points,
    /// unrounded, before the formula (for <c>[score]</c> and <c>[percent]</c>).
    /// </summary>
    /// <param name="definition">The definition, whose <see cref="SurveyScoringSettings.ScoreFormula"/> is evaluated.</param>
    /// <param name="response">The response being scored.</param>
    /// <returns>The total score, or <see langword="null"/> to use the sum.</returns>
    decimal? Evaluate(SurveyDefinition definition, SurveyResponse response);
}
