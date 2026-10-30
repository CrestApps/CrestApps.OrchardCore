using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow when a booking is confirmed, such as when its order is paid or the store accepts a request.
/// </summary>
public sealed class BookingConfirmedEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingConfirmedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingConfirmedEvent(IStringLocalizer<BookingConfirmedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingConfirmedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Confirmed"];
}
