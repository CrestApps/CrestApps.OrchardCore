using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// The outcome of validating a returned Stripe hosted-checkout session against the local subscription
/// session that is being finalized.
/// </summary>
public enum CheckoutReturnValidation
{
    /// <summary>The checkout is confirmed, paid and provably bound to the local session.</summary>
    Valid,

    /// <summary>The checkout is missing, unpaid, lacks a subscription, or does not reference the local session.</summary>
    NotConfirmed,

    /// <summary>The checkout was paid in a currency different from the invoice being fulfilled.</summary>
    CurrencyMismatch,
}

/// <summary>
/// Validates that a returned Stripe Checkout Session may finalize a local subscription session. The rules
/// here are security-critical: a hosted checkout id is supplied by the redirected browser and must never
/// be trusted without proving, via Stripe, that the session is complete, paid and bound to THIS local
/// session (through the client reference id we set when creating the checkout).
/// </summary>
public static class HostedCheckoutReturnValidator
{
    public static CheckoutReturnValidation Validate(CheckoutSessionDetails details, string localSessionId, string invoiceCurrency)
    {
        if (details == null ||
            !details.IsPaid ||
            string.IsNullOrEmpty(details.SubscriptionId) ||
            string.IsNullOrEmpty(localSessionId) ||
            !string.Equals(details.ClientReferenceId, localSessionId, StringComparison.Ordinal))
        {
            return CheckoutReturnValidation.NotConfirmed;
        }

        if (!string.IsNullOrEmpty(invoiceCurrency) &&
            !string.Equals(details.Currency, invoiceCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return CheckoutReturnValidation.CurrencyMismatch;
        }

        return CheckoutReturnValidation.Valid;
    }
}
