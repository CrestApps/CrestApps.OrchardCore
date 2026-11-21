using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts when a learner opens the first item of a course.
/// </summary>
public sealed class CourseStartedEvent : LearningEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CourseStartedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CourseStartedEvent(IStringLocalizer<CourseStartedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CourseStartedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Course Started"];
}
