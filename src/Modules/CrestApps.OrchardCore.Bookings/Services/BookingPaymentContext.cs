using CrestApps.OrchardCore.Bookings.Models;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// A payment, declined charge or refund for the bookings of one order, as an <see cref="IBookingPaymentHandler"/> is told
/// about it.
/// </summary>
public sealed class BookingPaymentContext
{
    /// <summary>
    /// Gets or sets the bookings the money is for: the order's bookings it applies to.
    /// </summary>
    public IReadOnlyList<Booking> Bookings { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the order the bookings were placed with.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the number of the order the bookings were placed with.
    /// </summary>
    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets what the payment paid for, or <see langword="null"/> for a refund.
    /// </summary>
    public BookingPaymentKind? PaymentKind { get; set; }

    /// <summary>
    /// Gets or sets the amount paid, charged or refunded, for the whole order rather than for each booking.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of <see cref="Amount"/>.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets why the charge failed or the money was refunded, or <see langword="null"/>.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets whether a failed charge is not tried again, so the buyer has to pay another way.
    /// </summary>
    public bool IsFinal { get; set; }

    /// <summary>
    /// Gets or sets what identifies the money: the order's checkout session for an order's payment, the balance's ledger
    /// transaction for a balance payment or a failed charge, and the refund's identifier for a refund.
    /// </summary>
    public string Reference { get; set; }
}
