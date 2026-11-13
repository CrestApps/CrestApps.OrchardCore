using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts when a learner completes a course.
/// </summary>
public sealed class CourseCompletedEvent : LearningEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCompletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CourseCompletedEvent(IStringLocalizer<CourseCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CourseCompletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Course Completed"];
}
