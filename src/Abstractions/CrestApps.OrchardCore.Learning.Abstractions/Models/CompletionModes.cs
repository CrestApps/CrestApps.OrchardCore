namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The completion modes of course items. A kind lists the modes it supports; the value is an open string.
/// </summary>
public static class CompletionModes
{
    /// <summary>
    /// Completed when the learner opens the item.
    /// </summary>
    public const string Viewed = "Viewed";

    /// <summary>
    /// Completed when the learner marks the item done.
    /// </summary>
    public const string MarkedDone = "MarkedDone";

    /// <summary>
    /// Completed when the learner has spent the minimum time on the item.
    /// </summary>
    public const string MinimumTime = "MinimumTime";

    /// <summary>
    /// Completed when the learner has watched the required share of a video.
    /// </summary>
    public const string Watched = "Watched";

    /// <summary>
    /// Completed when the learner confirms the statement of an acknowledgement.
    /// </summary>
    public const string Acknowledged = "Acknowledged";

    /// <summary>
    /// Completed when the learner hands in work.
    /// </summary>
    public const string Submitted = "Submitted";

    /// <summary>
    /// Completed when the learner's work is graded.
    /// </summary>
    public const string Graded = "Graded";

    /// <summary>
    /// Completed when the learner's score reaches the item's pass mark.
    /// </summary>
    public const string Passed = "Passed";

    /// <summary>
    /// Completed when the learner attends a class.
    /// </summary>
    public const string Attended = "Attended";

    /// <summary>
    /// Completed when a learning package reports it, marked self-reported.
    /// </summary>
    public const string PackageReported = "PackageReported";

    /// <summary>
    /// Completed when the learner's score reaches the item's pass mark, whatever the item reports about passing (Moodle's
    /// "viewed, graded and a score of at least N").
    /// </summary>
    public const string MinimumScore = "MinimumScore";
}
