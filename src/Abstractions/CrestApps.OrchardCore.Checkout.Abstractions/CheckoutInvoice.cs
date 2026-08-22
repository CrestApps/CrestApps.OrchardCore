using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Checkout.Json;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The authoritative summary of everything a checkout collects: the line items, the amounts due now,
/// the recurring obligations, and the tax determined for the amounts due now. A single invoice is built
/// for the whole checkout so a customer is charged exactly once regardless of how many flow steps
/// contributed billing items.
/// </summary>
public sealed class CheckoutInvoice
{
    /// <summary>
    /// The ISO-4217 currency code for every amount on the invoice.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The one-time amount due now (setup fees, one-off goods), before tax.
    /// </summary>
    public decimal? InitialPaymentAmount { get; set; }

    /// <summary>
    /// The amount of the first recurring cycle charged now, before tax.
    /// </summary>
    public decimal? FirstRecurringPaymentAmount { get; set; }

    /// <summary>
    /// The total amount collected now, before tax.
    /// </summary>
    public decimal DueNow { get; set; }

    /// <summary>
    /// The tax charged on the amount due now, determined by the taxation framework. This is <c>0</c> when
    /// the Taxation feature is disabled.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// The detailed tax lines that explain <see cref="TaxAmount"/>. Each jurisdiction/tax is preserved.
    /// </summary>
    public IList<TaxLine> TaxLines { get; set; }

    /// <summary>
    /// The immutable tax determination captured for the amount due now. Persisted with the transaction so
    /// historical tax is never recalculated with current rules.
    /// </summary>
    public TaxSnapshot TaxSnapshot { get; set; }

    /// <summary>
    /// The tax category code resolved at checkout, persisted so recurring cycles reuse the classification.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// The tax classification code resolved at checkout, persisted so recurring cycles reuse it.
    /// </summary>
    public string TaxClassificationCode { get; set; }

    /// <summary>
    /// The grand total collected now, including tax.
    /// </summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// The per-interval subtotals for the recurring obligations, before tax.
    /// </summary>
    [JsonConverter(typeof(BillingDurationKeyDictionaryJsonConverter))]
    public Dictionary<BillingDurationKey, decimal> Subtotals { get; set; }

    /// <summary>
    /// Every priced line on the invoice.
    /// </summary>
    public CheckoutLineItem[] LineItems { get; set; }

    /// <summary>
    /// Groups the recurring line items by their billing interval so each interval becomes a single
    /// recurring obligation with a unified expiration date.
    /// </summary>
    public Dictionary<BillingDurationKey, IList<CheckoutLineItem>> GetRecurringGroups()
    {
        var groups = new Dictionary<BillingDurationKey, IList<CheckoutLineItem>>();

        foreach (var lineItem in LineItems ?? [])
        {
            if (lineItem.Plan == null)
            {
                continue;
            }

            var key = new BillingDurationKey(lineItem.Plan.DurationType, lineItem.Plan.BillingDuration);

            if (!groups.TryGetValue(key, out var group))
            {
                group = new List<CheckoutLineItem>();
                groups[key] = group;
            }

            group.Add(lineItem);
        }

        return groups;
    }
}
