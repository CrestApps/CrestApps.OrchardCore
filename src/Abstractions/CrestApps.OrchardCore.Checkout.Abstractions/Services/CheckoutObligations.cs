using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Defines the stable obligation identifiers that a checkout must settle. An obligation is a single unit
/// of money the checkout has to collect (the one-time amount, or one recurring interval). The payment
/// endpoints stamp each durable <see cref="Models.PaymentAttempt"/> with the obligation it settles, and
/// the completion path asks the reconciliation service to confirm that every obligation the invoice
/// expects is backed by a verified, succeeded attempt. Centralizing the scheme guarantees the endpoints,
/// providers, and completion logic always agree on what "fully paid" means.
/// </summary>
public static class CheckoutObligations
{
    /// <summary>
    /// The obligation id for the single up-front, one-time amount due now.
    /// </summary>
    public const string OneTime = "onetime";

    /// <summary>
    /// Builds the obligation id for a recurring billing interval so every line item that shares the
    /// interval is settled as one obligation.
    /// </summary>
    /// <param name="key">The recurring billing interval.</param>
    public static string Recurring(BillingDurationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return $"recurring:{(int)key.Type}:{key.Duration}";
    }

    /// <summary>
    /// Returns the obligations the supplied invoice must settle to be considered fully paid: the one-time
    /// amount when it is greater than zero, plus one obligation for every distinct recurring interval.
    /// </summary>
    /// <param name="invoice">The checkout invoice.</param>
    public static IReadOnlyList<string> GetExpectedObligationIds(CheckoutInvoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var obligations = new List<string>();

        if (invoice.InitialPaymentAmount.HasValue && Money.IsGreaterThan(invoice.InitialPaymentAmount.Value, 0, invoice.Currency))
        {
            obligations.Add(OneTime);
        }

        foreach (var group in invoice.GetRecurringGroups())
        {
            // Only a recurring interval that actually collects money is an obligation. A zero-value
            // interval (for example a free plan) never produces a payment, so creating an obligation for it
            // would leave the checkout waiting forever for a payment that can never arrive.
            var intervalTotal = group.Value.Sum(lineItem => lineItem.GetLineTotal(invoice.Currency));

            if (!Money.IsGreaterThan(intervalTotal, 0, invoice.Currency))
            {
                continue;
            }

            obligations.Add(Recurring(group.Key));
        }

        return obligations;
    }
}
