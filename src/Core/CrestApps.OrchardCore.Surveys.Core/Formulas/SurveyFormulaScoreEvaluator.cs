using System.Globalization;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// Computes a survey's total score from its score formula over the category scores, with the survey formula service.
/// </summary>
public sealed class SurveyFormulaScoreEvaluator : ISurveyScoreFormulaEvaluator
{
    private readonly SurveyFormulaService _formulas;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyFormulaScoreEvaluator"/> class.
    /// </summary>
    /// <param name="formulas">The formula service.</param>
    public SurveyFormulaScoreEvaluator(SurveyFormulaService formulas)
    {
        _formulas = formulas;
    }

    /// <inheritdoc />
    public decimal? Evaluate(SurveyDefinition definition, IReadOnlyDictionary<string, decimal> categoryScores, decimal total)
    {
        if (string.IsNullOrWhiteSpace(definition?.Scoring?.ScoreFormula))
        {
            return null;
        }

        var response = new SurveyResponse { Score = total };

        foreach (var score in categoryScores ?? new Dictionary<string, decimal>())
        {
            response.CategoryScores[score.Key] = score.Value;
        }

        return _formulas.Evaluate(definition, response, definition.Scoring.ScoreFormula) switch
        {
            decimal value => value,
            long value => value,
            int value => value,
            double value => (decimal)value,
            string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }
}
