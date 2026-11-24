using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts when a learner completes an item of a course.
/// </summary>
public sealed class CourseItemCompletedEvent : LearningEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CourseItemCompletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CourseItemCompletedEvent(IStringLocalizer<CourseItemCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CourseItemCompletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Course Item Completed"];
}
