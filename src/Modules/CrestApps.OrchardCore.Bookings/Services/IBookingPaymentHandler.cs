namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Hears about money moving for bookings: an order's bookings paid in full or by deposit, the balance after a deposit
/// paid, a balance charge declined, and a refund recorded on a booked order. Each is reported once for each payment,
/// declined charge or refund, inside the unit of work that records it; a handler that throws is logged and does not undo
/// it. The workflow integration raises the booking payment workflow events once the change is committed.
/// </summary>
public interface IBookingPaymentHandler
{
    /// <summary>
    /// Runs when a payment toward bookings was received.
    /// </summary>
    /// <param name="context">The bookings and the payment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task PaymentReceivedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs when charging what's owed on bookings failed.
    /// </summary>
    /// <param name="context">The bookings, the charge and why it failed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task PaymentFailedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs when money paid for bookings was given back.
    /// </summary>
    /// <param name="context">The bookings and the refund.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RefundedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default);
}
