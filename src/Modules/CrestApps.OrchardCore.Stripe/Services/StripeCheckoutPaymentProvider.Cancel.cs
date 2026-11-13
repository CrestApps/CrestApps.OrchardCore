using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed partial class StripeCheckoutPaymentProvider
{
    private const string AwaitingCardStatus = "requires_payment_method";

    // Cancels a payment only while Stripe says it took nothing and waits for another card, the state a decline leaves it
    // in (CancelPaymentContext.OnlyIfUnpaid). Never refunds: a payment that succeeded, is processing, holds an
    // authorization or waits for the buyer to authenticate is left alone, whatever the local attempt says. One paid between
    // the read and the cancel stays paid too, because Stripe cancels no PaymentIntent that succeeded. CancelAsync catches
    // what Stripe throws.
    private async Task<PaymentCancelResult> CancelUnpaidAsync(PaymentAttempt attempt)
    {
        if (IsSubscriptionReference(attempt.ProviderReference))
        {
            // A subscription waits for its first payment while it is incomplete; once that invoice is paid it is active.
            var subscription = await _subscriptionService.GetAsync(attempt.ProviderReference);

            if (subscription is null || subscription.LatestInvoicePaid || !string.Equals(subscription.Status, "incomplete", StringComparison.Ordinal))
            {
                return PaymentCancelResult.Failure($"The Stripe subscription is '{subscription?.Status}', not waiting for its first payment.");
            }

            // Canceling the subscription leaves its open first invoice payable, so that invoice is voided too, which ends
            // the client secret the cards were tried against.
            return await CancelSubscriptionAsync(attempt, voidOpenInvoice: true);
        }

        var intent = await _paymentIntentService.RetrieveAsync(new RetrievePaymentIntentRequest
        {
            PaymentIntentId = attempt.ProviderReference,
        });

        if (intent.Status == "canceled")
        {
            return PaymentCancelResult.Success();
        }

        if (intent.Status != AwaitingCardStatus)
        {
            return PaymentCancelResult.Failure($"The Stripe PaymentIntent is '{intent.Status}', not waiting for another card.");
        }

        await _paymentIntentService.CancelAsync(new CancelPaymentIntentRequest
        {
            PaymentIntentId = intent.Id,
            CancellationReason = "abandoned",
            IdempotencyKey = "cancel_" + attempt.ItemId,
        });

        return PaymentCancelResult.Success();
    }

    private async Task<PaymentCancelResult> CancelSubscriptionAsync(PaymentAttempt attempt, bool voidOpenInvoice = false)
    {
        try
        {
            await _subscriptionService.CancelAsync(new CancelSubscriptionRequest
            {
                SubscriptionId = attempt.ProviderReference,
                AtPeriodEnd = false,
                Reason = voidOpenInvoice
                    ? "Too many cards were declined for the first payment of this subscription."
                    : "The checkout this subscription belonged to was rolled back.",
                IdempotencyKey = "cancel_" + attempt.ItemId,
                VoidOpenInvoice = voidOpenInvoice,
            });

            return PaymentCancelResult.Success();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to cancel Stripe subscription '{SubscriptionId}' for checkout attempt '{AttemptId}'.", attempt.ProviderReference, attempt.ItemId);

            return PaymentCancelResult.Failure("The Stripe subscription could not be canceled.");
        }
    }
}
