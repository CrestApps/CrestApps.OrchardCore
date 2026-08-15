using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Taxation.Models;
using Json;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class Invoice
{
    public string Currency { get; set; }

    public double? InitialPaymentAmount { get; set; }

    public double? FirstSubscriptionPaymentAmount { get; set; }

    public double DueNow { get; set; }

    /// <summary>
    /// The tax charged on the amount due now, determined by the taxation framework. This is
    /// <c>0</c> when the Taxation feature is disabled.
    /// </summary>
    public double TaxAmount { get; set; }

    /// <summary>
    /// The detailed tax lines that explain <see cref="TaxAmount"/>. Never collapse multiple tax
    /// lines into a single number; each jurisdiction/tax is preserved here.
    /// </summary>
    public IList<TaxLine> TaxLines { get; set; }

    /// <summary>
    /// The immutable tax determination captured for the amount due now. This snapshot is persisted
    /// with the transaction so historical tax is never recalculated with current rules.
    /// </summary>
    public TaxSnapshot TaxSnapshot { get; set; }

    /// <summary>
    /// The tax category code resolved at checkout. Persisted so recurring billing cycles can reuse the
    /// same classification when redetermining tax with current rules.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// The tax classification code resolved at checkout. Persisted so recurring billing cycles can reuse
    /// the same classification when redetermining tax with current rules.
    /// </summary>
    public string TaxClassificationCode { get; set; }

    public double GrandTotal { get; set; }

    public int? BillingCycles { get; set; }

    [JsonConverter(typeof(BillingDurationKeyDictionaryJsonConverter))]
    public Dictionary<BillingDurationKey, double> Subtotals { get; set; }

    public InvoiceLineItem[] LineItems { get; set; }

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
