using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow for each confirmed booking of an order when a refund is recorded on the order, whether the booking was
/// canceled with a refund or staff refunded the order. Besides the keys of <see cref="BookingEvent"/>, the input carries
/// <c>Amount</c> (the refund, for the whole order), <c>Currency</c>, <c>PaymentKind</c> (empty), <c>Reason</c> (the
/// refund's reason), <c>IsFinal</c> (<see langword="false"/>) and <c>PaymentReference</c> (the refund's identifier).
/// </summary>
public sealed class BookingRefundedEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingRefundedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingRefundedEvent(IStringLocalizer<BookingRefundedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingRefundedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Refunded"];
}
