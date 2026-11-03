using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow when a booking's hold runs out because its order wasn't paid in time.
/// </summary>
public sealed class BookingExpiredEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingExpiredEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingExpiredEvent(IStringLocalizer<BookingExpiredEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingExpiredEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Expired"];
}
