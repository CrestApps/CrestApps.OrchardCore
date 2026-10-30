using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts when a customer is marked as not coming.
/// </summary>
public sealed class AppointmentNoShowEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentNoShowEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentNoShowEvent(IStringLocalizer<AppointmentNoShowEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AppointmentNoShowEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Appointment Marked No-Show"];
}
