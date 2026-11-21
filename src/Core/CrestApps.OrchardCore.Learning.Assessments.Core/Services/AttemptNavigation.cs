using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Which questions of an attempt take an answer now. A free quiz takes an answer to any question; a sequential quiz
/// only to the questions of the page the learner is on, so the learner can neither read ahead nor go back. A question
/// checked with immediate feedback, and a description, take none.
/// </summary>
public static class AttemptNavigation
{
    /// <summary>
    /// Gets whether a question takes an answer now.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="index">The question position.</param>
    /// <param name="sequential">Whether the quiz is answered one page after another.</param>
    /// <returns><see langword="true"/> when it may be answered.</returns>
    public static bool CanAnswer(AssessmentAttempt attempt, int index, bool sequential)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (index < 0 || index >= attempt.Items.Count)
        {
            return false;
        }

        var item = attempt.Items[index];

        if (item.CheckedUtc is not null || item.QuestionType == AssessmentsConstants.QuestionTypes.Description)
        {
            return false;
        }

        return !sequential || AttemptPaging.PageOf(attempt, index, sequential) == attempt.CurrentPage;
    }

    /// <summary>
    /// Gets whether the learner may move to a page: any page of a free quiz, and only the next page of a sequential one.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="page">The page, from zero.</param>
    /// <param name="sequential">Whether the quiz is answered one page after another.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public static bool CanGoTo(AssessmentAttempt attempt, int page, bool sequential)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return !sequential || page == attempt.CurrentPage + 1;
    }
}
