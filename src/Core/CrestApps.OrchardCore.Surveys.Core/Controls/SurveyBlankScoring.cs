using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Scores blank answers: each blank whose value is one of its accepted answers earns its share of the points.
/// </summary>
public static class SurveyBlankScoring
{
    /// <summary>
    /// Scores blank answers.
    /// </summary>
    /// <param name="key">The accepted answers of each blank.</param>
    /// <param name="answer">The answer, one value per blank.</param>
    /// <param name="max">The points possible.</param>
    /// <param name="caseSensitive">Whether case matters.</param>
    /// <returns>The result.</returns>
    public static SurveyScoreResult Score(IReadOnlyList<IReadOnlyList<string>> key, SurveyAnswer answer, decimal max, bool caseSensitive)
    {
        if (key is null || key.Count == 0)
        {
            return SurveyScoreResult.Of(0, max);
        }

        var given = answer?.Values ?? [];
        var right = key.Where((accepted, index) => index < given.Count && accepted.Any(value => SurveyScoringMath.Same(value, given[index], caseSensitive))).Count();

        return new SurveyScoreResult { Points = SurveyScoringMath.Points(max * right / key.Count, max), MaxPoints = max, Correct = right == key.Count };
    }
}
