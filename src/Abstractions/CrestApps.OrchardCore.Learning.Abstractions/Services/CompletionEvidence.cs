using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The rules that tell whether a completed enrollment may support something the site issues on it, such as a
/// certificate or a digital credential. Learning owns them, because they read the enrollment and its completion
/// snapshot; every module that issues on a completion calls them.
/// </summary>
public static class CompletionEvidence
{
    /// <summary>
    /// Tells whether an enrollment may support what is issued on it: a failed one never does, and one under review does
    /// not while staff are deciding.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <returns><see langword="true"/> when it counts.</returns>
    public static bool CountsAsEvidence(Enrollment enrollment)
        => enrollment is not null && enrollment.Passed != false && !enrollment.NeedsReview;

    /// <summary>
    /// Tells whether a completed enrollment may support what is issued on it, such as a digital credential, education
    /// credits or a learning path's credential: it is completed, it counts as evidence (<see cref="CountsAsEvidence"/>)
    /// and it does not rest on a result the browser reported (<see cref="RestsOnABrowserResult(Enrollment)"/>). Every
    /// module that issues on a completion, directly or through a learning path, asks this.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <returns><see langword="true"/> when the completion may support what is issued on it.</returns>
    public static bool SupportsIssuing(Enrollment enrollment)
        => enrollment is { Status: EnrollmentStatus.Completed } && CountsAsEvidence(enrollment) && !RestsOnABrowserResult(enrollment);

    /// <summary>
    /// Tells whether a completion rests on a result the browser reported, such as a learning package's own status or
    /// score: the result of a required item of the completion snapshot was reported by the browser and its item did not
    /// let such a result count. The browser can be changed by the learner, so by default such a completion supports
    /// nothing the site issues. A share of a page or video viewed is not a result and does not count here.
    /// </summary>
    /// <param name="enrollment">The completed enrollment.</param>
    /// <returns><see langword="true"/> when the completion cannot support what is issued on it.</returns>
    public static bool RestsOnABrowserResult(Enrollment enrollment)
        => RestsOnABrowserResult(enrollment?.Completion);

    /// <summary>
    /// Tells whether a completion snapshot rests on a result the browser reported for a required item whose item did not
    /// allow it.
    /// </summary>
    /// <param name="snapshot">The completion snapshot.</param>
    /// <returns><see langword="true"/> when the completion cannot support what is issued on it.</returns>
    public static bool RestsOnABrowserResult(CompletionSnapshot snapshot)
        => snapshot?.Items is { } items
        && items.Any(item => item is not null && item.Required && item.ResultSelfReported && !item.AllowSelfReported);
}
