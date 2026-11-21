namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Hears when a learner starts and completes a course item, so a kind can react to its own items. Every member has an
/// empty default.
/// </summary>
public interface ICourseItemHandler
{
    /// <summary>
    /// The learner opened a course item for the first time.
    /// </summary>
    /// <param name="context">The enrollment, the item and the progress on it.</param>
    /// <returns>A task.</returns>
    Task StartedAsync(CourseItemEventContext context)
        => Task.CompletedTask;

    /// <summary>
    /// The learner completed a course item.
    /// </summary>
    /// <param name="context">The enrollment, the item and the progress on it.</param>
    /// <returns>A task.</returns>
    Task CompletedAsync(CourseItemEventContext context)
        => Task.CompletedTask;
}
