namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// Turns the scores of a learner's attempts on an item into the item's score, by a grading method. Quizzes and learning
/// packages share it.
/// </summary>
public static class AttemptScores
{
    /// <summary>
    /// Combines the scores of the graded attempts, in the order they were made.
    /// </summary>
    /// <param name="scores">The scores, as percentages, oldest first.</param>
    /// <param name="method">The grading method.</param>
    /// <returns>The score, or <see langword="null"/> when no attempt has one.</returns>
    public static decimal? Combine(IReadOnlyList<decimal> scores, GradingMethod method)
    {
        if (scores is null || scores.Count == 0)
        {
            return null;
        }

        return method switch
        {
            GradingMethod.Latest => scores[^1],
            GradingMethod.First => scores[0],
            GradingMethod.Average => Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero),
            _ => scores.Max(),
        };
    }
}
