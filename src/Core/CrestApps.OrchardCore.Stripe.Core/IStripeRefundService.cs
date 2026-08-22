using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Provides access to the Stripe Refund API for refunding settled payments. It converts the requested
/// major-unit amount to the currency's minor units with <see cref="StripeCurrency"/> and honors the
/// supplied idempotency key so a retried refund never double-refunds at the gateway.
/// </summary>
public interface IStripeRefundService
{
    /// <summary>
    /// Creates a refund for a settled Stripe PaymentIntent.
    /// </summary>
    /// <param name="model">The refund request.</param>
    /// <returns>The authoritative refund details returned by Stripe.</returns>
    Task<RefundDetails> CreateAsync(CreateRefundRequest model);
}
