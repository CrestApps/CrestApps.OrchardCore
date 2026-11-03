using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow when a booking is made, held or requested, with its dates, status and customer.
/// </summary>
public sealed class BookingCreatedEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingCreatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingCreatedEvent(IStringLocalizer<BookingCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingCreatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Created"];
}
