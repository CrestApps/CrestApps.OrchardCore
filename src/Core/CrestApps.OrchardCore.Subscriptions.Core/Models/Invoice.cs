using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Taxation.Models;
using Json;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents the amounts, tax details, and line items calculated for a subscription checkout.
/// </summary>
public class Invoice
{
    /// <summary>
    /// Gets or sets the ISO currency code used by the invoice.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the one-time amount due at checkout before recurring subscription charges.
    /// </summary>
    public decimal? InitialPaymentAmount { get; set; }

    /// <summary>
    /// Gets or sets the first recurring subscription payment amount due at checkout.
    /// </summary>
    public decimal? FirstSubscriptionPaymentAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount due immediately for the invoice before tax is included in the grand total.
    /// </summary>
    public decimal DueNow { get; set; }

    /// <summary>
    /// Gets or sets the tax charged on the amount due now, determined by the taxation framework. This is
    /// <c>0</c> when the Taxation feature is disabled.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the detailed tax lines that explain <see cref="TaxAmount"/>. Never collapse multiple tax
    /// lines into a single number; each jurisdiction/tax is preserved here.
    /// </summary>
    public IList<TaxLine> TaxLines { get; set; }

    /// <summary>
    /// Gets or sets the immutable tax determination captured for the amount due now. This snapshot is persisted
    /// with the transaction so historical tax is never recalculated with current rules.
    /// </summary>
    public TaxSnapshot TaxSnapshot { get; set; }

    /// <summary>
    /// Gets or sets the tax category code resolved at checkout. Persisted so recurring billing cycles can reuse the
    /// same classification when redetermining tax with current rules.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the tax classification code resolved at checkout. Persisted so recurring billing cycles can reuse
    /// the same classification when redetermining tax with current rules.
    /// </summary>
    public string TaxClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets the full invoice amount including tax.
    /// </summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// Gets or sets the number of billing cycles represented by the invoice, when a limit applies.
    /// </summary>
    public int? BillingCycles { get; set; }

    /// <summary>
    /// Gets or sets subtotal amounts grouped by billing duration.
    /// </summary>
    [JsonConverter(typeof(BillingDurationKeyDictionaryJsonConverter))]
    public Dictionary<BillingDurationKey, decimal> Subtotals { get; set; }

    /// <summary>
    /// Gets or sets the invoice line items.
    /// </summary>
    public InvoiceLineItem[] LineItems { get; set; }

    /// <summary>
    /// Groups subscription line items by their billing duration.
    /// </summary>
    /// <returns>A dictionary of subscription line items keyed by billing duration.</returns>
    public Dictionary<BillingDurationKey, IList<InvoiceLineItem>> GetSubscriptionGroups()
    {
        var subscriptionGroups = new Dictionary<BillingDurationKey, IList<InvoiceLineItem>>();

        foreach (var lineItem in LineItems ?? [])
        {
            if (lineItem.Subscription == null)
            {
                // At this point, this isn't a subscription line item. Ignore it.
                continue;
            }

            var key = new BillingDurationKey(lineItem.Subscription.DurationType, lineItem.Subscription.BillingDuration);

            if (!subscriptionGroups.TryGetValue(key, out var group))
            {
                group = new List<InvoiceLineItem>();
                subscriptionGroups[key] = group;
            }

            group.Add(lineItem);
        }

        return subscriptionGroups;
    }
}
