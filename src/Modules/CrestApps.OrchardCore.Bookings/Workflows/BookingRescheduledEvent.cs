using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow when a booking moves to other dates.
/// </summary>
public sealed class BookingRescheduledEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingRescheduledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingRescheduledEvent(IStringLocalizer<BookingRescheduledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingRescheduledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Rescheduled"];
}
