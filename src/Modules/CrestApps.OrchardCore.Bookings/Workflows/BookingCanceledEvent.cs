using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow when a booking is canceled.
/// </summary>
public sealed class BookingCanceledEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingCanceledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingCanceledEvent(IStringLocalizer<BookingCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingCanceledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Canceled"];
}
