using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts when a cancellation frees a time a waitlisted customer wanted.
/// </summary>
public sealed class WaitlistSlotAvailableEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WaitlistSlotAvailableEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WaitlistSlotAvailableEvent(IStringLocalizer<WaitlistSlotAvailableEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(WaitlistSlotAvailableEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Waitlist Slot Available"];
}
