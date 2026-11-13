using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts when an enrollment moves to another status, such as suspended, completed, ended or withdrawn.
/// </summary>
public sealed class EnrollmentStatusChangedEvent : LearningEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentStatusChangedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EnrollmentStatusChangedEvent(IStringLocalizer<EnrollmentStatusChangedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(EnrollmentStatusChangedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Enrollment Status Changed"];
}
