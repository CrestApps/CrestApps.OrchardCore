namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// Where a learner stands on one course item.
/// </summary>
public enum CourseItemState
{
    /// <summary>
    /// The learner has not opened the item.
    /// </summary>
    NotStarted,

    /// <summary>
    /// The learner opened the item and has not met its completion mode.
    /// </summary>
    InProgress,

    /// <summary>
    /// The learner met the item's completion mode.
    /// </summary>
    Completed,

    /// <summary>
    /// The item is graded and the learner's result did not pass.
    /// </summary>
    Failed,
}
