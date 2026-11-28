using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Controls;

/// <summary>
/// Scores a number answer against the key's number and tolerance; shared by number and slider questions.
/// </summary>
public static class SurveyNumberScoring
{
    /// <summary>
    /// Scores a number answer.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer.</param>
    /// <param name="max">The points possible.</param>
    /// <param name="penalty">The penalty of a wrong answer.</param>
    /// <returns>The result.</returns>
    public static SurveyScoreResult Score(SurveyQuestion question, SurveyAnswer answer, decimal max, decimal penalty)
    {
        if (answer?.Number is not { } value)
        {
            return SurveyScoreResult.Of(0, max);
        }

        if (question?.Scoring?.NumberAnswer is not { } key)
        {
            return SurveyScoreResult.Of(0, max);
        }

        return IsWithin(value, key, question.Scoring.Tolerance)
            ? SurveyScoreResult.Of(max, max)
            : new SurveyScoreResult { Points = -penalty, MaxPoints = max, Correct = false };
    }

    /// <summary>
    /// Gets whether a number is within a tolerance of the key. A difference too large for a decimal is never within it,
    /// so a posted value near the decimal limits is just wrong instead of failing the scoring.
    /// </summary>
    /// <param name="value">The answer.</param>
    /// <param name="key">The right answer.</param>
    /// <param name="tolerance">The tolerance; its sign is ignored.</param>
    /// <returns><see langword="true"/> when the answer is within the tolerance.</returns>
    public static bool IsWithin(decimal value, decimal key, decimal tolerance)
    {
        try
        {
            return Math.Abs(value - key) <= Math.Abs(tolerance);
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
