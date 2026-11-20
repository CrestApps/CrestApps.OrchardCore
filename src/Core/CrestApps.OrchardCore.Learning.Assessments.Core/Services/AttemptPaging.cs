using CrestApps.OrchardCore.Learning.Assessments.Models;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Splits the questions of an attempt into pages. The page size is copied onto the attempt when it starts; without one,
/// a free quiz shows every question on one page and a sequential quiz one question a page.
/// </summary>
public static class AttemptPaging
{
    /// <summary>
    /// Gets how many questions a page shows.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="sequential">Whether the quiz is answered one page after another.</param>
    /// <returns>The page size, at least one.</returns>
    public static int PageSize(AssessmentAttempt attempt, bool sequential)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (attempt.QuestionsPerPage > 0)
        {
            return attempt.QuestionsPerPage;
        }

        return sequential ? 1 : Math.Max(attempt.Items.Count, 1);
    }

    /// <summary>
    /// Gets how many pages the attempt has.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="sequential">Whether the quiz is answered one page after another.</param>
    /// <returns>The number of pages, at least one.</returns>
    public static int PageCount(AssessmentAttempt attempt, bool sequential)
    {
        var size = PageSize(attempt, sequential);

        return Math.Max((attempt.Items.Count + size - 1) / size, 1);
    }

    /// <summary>
    /// Gets the page a question is on.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="index">The question position.</param>
    /// <param name="sequential">Whether the quiz is answered one page after another.</param>
    /// <returns>The page, from zero.</returns>
    public static int PageOf(AssessmentAttempt attempt, int index, bool sequential)
        => Math.Max(index, 0) / PageSize(attempt, sequential);

    /// <summary>
    /// Gets the questions of a page.
    /// </summary>
    /// <param name="attempt">The attempt.</param>
    /// <param name="page">The page, from zero; it is kept within the pages.</param>
    /// <param name="sequential">Whether the quiz is answered one page after another.</param>
    /// <returns>The questions.</returns>
    public static IList<AttemptItem> ItemsOn(AssessmentAttempt attempt, int page, bool sequential)
    {
        var size = PageSize(attempt, sequential);
        var current = Math.Clamp(page, 0, PageCount(attempt, sequential) - 1);

        return attempt.Items.Skip(current * size).Take(size).ToList();
    }
}
