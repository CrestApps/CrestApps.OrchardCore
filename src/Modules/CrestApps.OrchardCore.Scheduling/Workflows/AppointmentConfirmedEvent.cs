using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts when an appointment is booked or confirmed.
/// </summary>
public sealed class AppointmentConfirmedEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentConfirmedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentConfirmedEvent(IStringLocalizer<AppointmentConfirmedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AppointmentConfirmedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Appointment Confirmed"];
}
