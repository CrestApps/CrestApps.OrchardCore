using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts when a learner is enrolled in a course, or a request to enroll is approved.
/// </summary>
public sealed class LearnerEnrolledEvent : LearningEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LearnerEnrolledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearnerEnrolledEvent(IStringLocalizer<LearnerEnrolledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(LearnerEnrolledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Learner Enrolled"];
}
