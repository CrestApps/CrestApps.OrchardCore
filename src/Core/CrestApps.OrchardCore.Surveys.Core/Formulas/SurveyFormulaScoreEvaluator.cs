using System.Globalization;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// Computes a survey's total score from its score formula, with the survey formula service, over the response being
/// scored: its category scores, its question scores, and its sum of points and percent before the formula.
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
    public decimal? Evaluate(SurveyDefinition definition, SurveyResponse response)
    {
        if (string.IsNullOrWhiteSpace(definition?.Scoring?.ScoreFormula) || response is null)
        {
            return null;
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
