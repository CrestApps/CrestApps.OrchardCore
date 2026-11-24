namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The states My learning filters the learner's courses by.
/// </summary>
public static class LearnerCourseStates
{
    /// <summary>
    /// Every course.
    /// </summary>
    public const string All = "all";

    /// <summary>
    /// Started and not completed.
    /// </summary>
    public const string InProgress = "inprogress";

    /// <summary>
    /// Not started yet: no item opened, waiting for approval, or before the course's start date.
    /// </summary>
    public const string NotStarted = "notstarted";

    /// <summary>
    /// Completed.
    /// </summary>
    public const string Completed = "completed";

    /// <summary>
    /// The states a learner can filter by, in the order of the filter.
    /// </summary>
    public static readonly string[] Filters = [All, InProgress, NotStarted, Completed];
}
