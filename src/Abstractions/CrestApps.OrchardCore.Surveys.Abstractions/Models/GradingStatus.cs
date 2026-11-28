namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Where grading of a response stands.
/// </summary>
public enum GradingStatus
{
    /// <summary>
    /// The survey is not scored.
    /// </summary>
    NotScored,

    /// <summary>
    /// A question needs a grader.
    /// </summary>
    Pending,

    /// <summary>
    /// Every question has a score.
    /// </summary>
    Graded,
}
