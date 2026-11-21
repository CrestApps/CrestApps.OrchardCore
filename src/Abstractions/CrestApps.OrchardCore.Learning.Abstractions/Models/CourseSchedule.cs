namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// Where a course is between its start and end dates.
/// </summary>
public enum CourseSchedule
{
    /// <summary>
    /// The course has started, or has no start date, and has not ended.
    /// </summary>
    Open,

    /// <summary>
    /// The course starts later: learners see it and may enroll, but cannot open its items yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// The course's end date has passed. It stays open; it is shown as ended instead of in progress.
    /// </summary>
    Ended,
}
