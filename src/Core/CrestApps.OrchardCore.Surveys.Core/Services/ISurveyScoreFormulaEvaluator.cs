using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Evaluates a survey's score formula over its category scores. The default returns <see langword="null"/>, so the
/// total is the sum of the question points; the formula services replace it.
/// </summary>
public interface ISurveyScoreFormulaEvaluator
{
    /// <summary>
    /// Evaluates the score formula of a definition.
    /// </summary>
    /// <param name="definition">The definition, whose <see cref="SurveyScoringSettings.ScoreFormula"/> is evaluated.</param>
    /// <param name="categoryScores">The score of each category, by category name.</param>
    /// <param name="total">The sum of the question points.</param>
    /// <returns>The total score, or <see langword="null"/> to use the sum.</returns>
    decimal? Evaluate(SurveyDefinition definition, IReadOnlyDictionary<string, decimal> categoryScores, decimal total);
}
