using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow when a booking is completed.
/// </summary>
public sealed class BookingCompletedEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingCompletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingCompletedEvent(IStringLocalizer<BookingCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingCompletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Completed"];
}
