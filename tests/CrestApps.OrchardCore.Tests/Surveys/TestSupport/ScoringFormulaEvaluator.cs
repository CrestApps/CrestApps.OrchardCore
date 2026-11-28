using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A score formula for tests: twice the category named "skill", read by its id as the real evaluator reads it.
/// </summary>
internal sealed class ScoringFormulaEvaluator : ISurveyScoreFormulaEvaluator
{
    public decimal? Evaluate(SurveyDefinition definition, SurveyResponse response)
        => definition.ScoreCategories?.FirstOrDefault(category => category.Name == "skill") is { } skill
            && response.CategoryScores.TryGetValue(skill.Id, out var score)
                ? score * 2
                : null;
}
