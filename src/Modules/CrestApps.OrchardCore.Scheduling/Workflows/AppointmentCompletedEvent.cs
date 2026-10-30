using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts when an appointment is marked as taken place.
/// </summary>
public sealed class AppointmentCompletedEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentCompletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentCompletedEvent(IStringLocalizer<AppointmentCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AppointmentCompletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Appointment Completed"];
}
