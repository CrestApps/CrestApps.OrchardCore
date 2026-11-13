using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts when a learner asks to enroll in a course that needs approval.
/// </summary>
public sealed class EnrollmentRequestedEvent : LearningEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentRequestedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EnrollmentRequestedEvent(IStringLocalizer<EnrollmentRequestedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(EnrollmentRequestedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Enrollment Requested"];
}
