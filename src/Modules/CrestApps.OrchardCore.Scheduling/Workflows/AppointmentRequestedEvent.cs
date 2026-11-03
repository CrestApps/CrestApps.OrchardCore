using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts when an appointment is requested and waits for confirmation.
/// </summary>
public sealed class AppointmentRequestedEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentRequestedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentRequestedEvent(IStringLocalizer<AppointmentRequestedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AppointmentRequestedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Appointment Requested"];
}
