using System.Text.Json.Serialization;
using Json;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class Invoice
{
    public string Currency { get; set; }

    public double? InitialPaymentAmount { get; set; }

    public double? FirstSubscriptionPaymentAmount { get; set; }

    public double DueNow { get; set; }

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
