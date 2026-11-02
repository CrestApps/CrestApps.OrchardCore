using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow for each booking when charging its order's balance to the card kept from the deposit fails. Besides
/// the keys of <see cref="BookingEvent"/>, the input carries <c>Amount</c> (the balance, for the whole order),
/// <c>Currency</c>, <c>PaymentKind</c> (<c>Balance</c>), <c>Reason</c> (why the charge failed), <c>IsFinal</c>
/// (<see langword="true"/> when the charge is not tried again, so the buyer pays from the invoice's pay link) and
/// <c>PaymentReference</c> (the balance's ledger transaction).
/// </summary>
public sealed class BookingPaymentFailedEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingPaymentFailedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingPaymentFailedEvent(IStringLocalizer<BookingPaymentFailedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingPaymentFailedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Payment Failed"];
}
