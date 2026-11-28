using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Leaves the total score as the sum of the question points.
/// </summary>
public sealed class NullSurveyScoreFormulaEvaluator : ISurveyScoreFormulaEvaluator
{
    /// <inheritdoc />
    public decimal? Evaluate(SurveyDefinition definition, SurveyResponse response)
        => null;
}
