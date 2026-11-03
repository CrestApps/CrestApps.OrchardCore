using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow when a booking is marked as a no-show.
/// </summary>
public sealed class BookingNoShowEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingNoShowEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingNoShowEvent(IStringLocalizer<BookingNoShowEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingNoShowEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking No-Show"];
}
