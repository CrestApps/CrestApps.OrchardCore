using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts when an appointment moves to another time.
/// </summary>
public sealed class AppointmentRescheduledEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentRescheduledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentRescheduledEvent(IStringLocalizer<AppointmentRescheduledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AppointmentRescheduledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Appointment Rescheduled"];
}
