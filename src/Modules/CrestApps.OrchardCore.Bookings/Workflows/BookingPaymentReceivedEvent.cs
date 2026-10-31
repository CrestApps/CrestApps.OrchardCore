using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// Starts a workflow for each booking a payment was received for: the order paid in full or by deposit, or the balance
/// after a deposit paid. Besides the keys of <see cref="BookingEvent"/>, the input carries <c>Amount</c> (the payment, for
/// the whole order), <c>Currency</c>, <c>PaymentKind</c> (<c>FullPayment</c>, <c>Deposit</c> or <c>Balance</c>),
/// <c>Reason</c> (empty), <c>IsFinal</c> (<see langword="false"/>) and <c>PaymentReference</c> (the order's checkout
/// session, or the balance's ledger transaction).
/// </summary>
public sealed class BookingPaymentReceivedEvent : BookingEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingPaymentReceivedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingPaymentReceivedEvent(IStringLocalizer<BookingPaymentReceivedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(BookingPaymentReceivedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Booking Payment Received"];
}
