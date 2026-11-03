using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts when an appointment is cancelled.
/// </summary>
public sealed class AppointmentCancelledEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentCancelledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentCancelledEvent(IStringLocalizer<AppointmentCancelledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AppointmentCancelledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Appointment Cancelled"];
}
