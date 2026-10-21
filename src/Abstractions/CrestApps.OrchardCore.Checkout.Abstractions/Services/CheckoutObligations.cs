using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;

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
    /// Gets the number of days before the first cycle of a recurring group is billed.
    /// </summary>
    /// <param name="lineItems">The line items that share one billing interval.</param>
    /// <returns>The longest deferral among the lines, or zero when the first cycle is billed now.</returns>
    /// <remarks>
    /// A free trial and a delayed start are the same thing to the money: nothing is collected now, a
    /// payment method is attached, and the gateway starts billing on a later date. The longest one wins,
    /// because billing a customer before the trial promised on any line has run out is what produces a
    /// chargeback.
    /// </remarks>
    public static int GetDeferralDays(IEnumerable<CheckoutLineItem> lineItems)
    {
        var days = 0;

        foreach (var lineItem in lineItems ?? [])
        {
            days = Math.Max(days, Math.Max(lineItem.Plan?.TrialDays ?? 0, lineItem.Plan?.StartDayDelay ?? 0));
        }

        return days;
    }

    /// <summary>
    /// Gets what one full cycle of a recurring group costs: the lines' totals, never what is due now. A
    /// first-cycle discount or a trial's zero applies to the first payment only.
    /// </summary>
    /// <param name="lineItems">The line items that share one billing interval.</param>
    /// <param name="currency">The checkout's currency.</param>
    public static decimal GetCycleAmount(IEnumerable<CheckoutLineItem> lineItems, string currency)
        => Money.Round((lineItems ?? []).Sum(lineItem => lineItem.GetLineTotal(currency)), currency);

    /// <summary>
    /// Gets how many cycles a recurring group bills at most, or <see langword="null"/> for no limit.
    /// </summary>
    /// <param name="lineItems">The line items that share one billing interval.</param>
    /// <returns>The lowest limit among the lines: billing past any line's limit charges for something the customer did not agree to.</returns>
    public static int? GetCycleLimit(IEnumerable<CheckoutLineItem> lineItems)
        => (lineItems ?? [])
            .Select(lineItem => lineItem.Plan?.BillingCycleLimit)
            .Where(limit => limit > 0)
            .DefaultIfEmpty(null)
            .Min();

    /// <summary>
    /// Finds the recurring group an obligation settles.
    /// </summary>
    /// <param name="invoice">The checkout invoice.</param>
    /// <param name="obligationId">The obligation id, as <see cref="Recurring(BillingDurationKey)"/> builds it.</param>
    /// <param name="interval">The group's billing interval, when found.</param>
    /// <param name="lineItems">The group's line items, when found.</param>
    /// <returns>Whether the obligation is one of the invoice's recurring groups.</returns>
    public static bool TryGetRecurringGroup(CheckoutInvoice invoice, string obligationId, out BillingDurationKey interval, out IList<CheckoutLineItem> lineItems)
    {
        interval = null;
        lineItems = null;

        if (invoice is null || string.IsNullOrEmpty(obligationId) || string.Equals(obligationId, OneTime, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var group in invoice.GetRecurringGroups())
        {
            if (string.Equals(Recurring(group.Key), obligationId, StringComparison.Ordinal))
            {
                interval = group.Key;
                lineItems = group.Value;

                return true;
            }
        }

        return false;
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
