using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// The rule of a quiz answered one question at a time: the learner sees and answers only the next unanswered question,
/// and cannot read ahead or go back to change an answer.
/// </summary>
public static class AttemptNavigation
{
    /// <summary>
    /// Gets the question a learner of a sequential quiz is on: the first one without an answer, or the last one when
    /// every question is answered.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <returns>The index of the question.</returns>
    public static int CurrentIndex(AssessmentAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return attempt.Items.FirstOrDefault(item => item.Response is null)?.Index ?? Math.Max(attempt.Items.Count - 1, 0);
    }

    /// <summary>
    /// Gets whether a learner of a sequential quiz may answer a question now: only the next unanswered one.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="index">The index of the question.</param>
    /// <returns><see langword="true"/> when it may be answered.</returns>
    public static bool CanAnswer(AssessmentAttempt attempt, int index)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return index >= 0 && index < attempt.Items.Count && attempt.Items[index].Response is null && index == CurrentIndex(attempt);
    }
}
