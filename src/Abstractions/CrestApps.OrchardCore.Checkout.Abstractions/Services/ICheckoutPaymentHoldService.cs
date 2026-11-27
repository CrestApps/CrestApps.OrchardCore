using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Answers whether the checkout that holds a record (an order, a transaction) may still take the money for it, so a
/// payment gate refuses a second checkout of the same record instead of letting the buyer pay twice.
/// </summary>
/// <remarks>
/// A gate checks and takes the hold under the record's own lock, and commits it before the lock is let go. The holder's
/// payment holds while it may still take the money: before it reached the provider (for
/// <see cref="CheckoutPaymentHold.BeginWindow"/>), while the provider hasn't answered (for
/// <see cref="CheckoutPaymentHold.PendingHoldLimit"/>, then it is canceled at the provider when it took nothing), and once it
/// succeeded until the record applied it. A declined or canceled payment, or a finished checkout, holds nothing.
/// </remarks>
public interface ICheckoutPaymentHoldService
{
    /// <summary>
    /// Returns whether another checkout's payment for the record may still take the money.
    /// </summary>
    /// <param name="hold">The hold kept on the record, or <see langword="null"/> when there is none.</param>
    /// <param name="sessionId">The checkout asking to pay.</param>
    /// <param name="isApplied">
    /// Whether the record already applied a succeeded attempt of the holder, such as a payment recorded on a transaction.
    /// An applied attempt holds nothing. <see langword="null"/> when the record applies nothing before its checkout
    /// completes.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> IsHeldByAnotherAsync(CheckoutPaymentHold hold, string sessionId, Func<PaymentAttempt, bool> isApplied = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the hold a checkout takes when its payment is let through. A checkout that already holds the record keeps
    /// the time it first took it: beginning again from the same checkout, every couple of minutes, must not keep the real
    /// payer out for good.
    /// </summary>
    /// <param name="current">The hold kept on the record, or <see langword="null"/> when there is none.</param>
    /// <param name="sessionId">The checkout taking the hold.</param>
    CheckoutPaymentHold Take(CheckoutPaymentHold current, string sessionId);
}
