using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Implements Stripe refund operations. It converts the requested major-unit amount to the currency's
/// minor units with <see cref="StripeCurrency"/> and forwards the idempotency key so a retried refund
/// never double-refunds at the gateway.
/// </summary>
public sealed class StripeRefundService : IStripeRefundService
{
    private readonly RefundService _refundService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeRefundService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to call the Stripe API.</param>
    public StripeRefundService(StripeClient stripeClient)
    {
        _refundService = new RefundService(stripeClient);
    }

    /// <inheritdoc/>
    public async Task<RefundDetails> CreateAsync(CreateRefundRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentIntentId);
        ArgumentException.ThrowIfNullOrEmpty(model.Currency);

        var refundOptions = new RefundCreateOptions
        {
            PaymentIntent = model.PaymentIntentId,

            // Currency-aware minor units so a zero- or three-decimal currency is never over- or
            // under-refunded by a hardcoded hundredths conversion.
            Amount = StripeCurrency.ToMinorUnits(model.Amount, model.Currency),
        };

        if (!string.IsNullOrEmpty(model.Reason))
        {
            refundOptions.Reason = model.Reason;
        }

        // Stamp the originating refund's idempotency key into the gateway metadata so an inbound webhook
        // can correlate this refund back to its local ledger entry even before the provider reference was
        // persisted, since Stripe does not echo the request idempotency key on the webhook payload.
        if (!string.IsNullOrEmpty(model.IdempotencyKey))
        {
            refundOptions.Metadata = new Dictionary<string, string>
            {
                [CheckoutRefundMetadataKeys.IdempotencyKey] = model.IdempotencyKey,
            };
        }

        var refund = await _refundService.CreateAsync(refundOptions, model.ToRequestOptions());

        return new RefundDetails
        {
            Id = refund.Id,
            Status = refund.Status,
            Amount = refund.Amount,
            Currency = refund.Currency,
            FailureReason = refund.FailureReason,
        };
    }
}
