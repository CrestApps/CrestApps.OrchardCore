using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts when a held time runs out before the booking is finished.
/// </summary>
public sealed class AppointmentHoldExpiredEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentHoldExpiredEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentHoldExpiredEvent(IStringLocalizer<AppointmentHoldExpiredEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AppointmentHoldExpiredEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Reservation Hold Expired"];
}
