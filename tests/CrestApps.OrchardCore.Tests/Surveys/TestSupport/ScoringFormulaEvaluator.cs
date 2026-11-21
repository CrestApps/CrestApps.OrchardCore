using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A score formula for tests: twice the named category.
/// </summary>
internal sealed class ScoringFormulaEvaluator : ISurveyScoreFormulaEvaluator
{
    public decimal? Evaluate(SurveyDefinition definition, IReadOnlyDictionary<string, decimal> categoryScores, decimal total)
        => categoryScores.TryGetValue("skill", out var skill) ? skill * 2 : null;
}
