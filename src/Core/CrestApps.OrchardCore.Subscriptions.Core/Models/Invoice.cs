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
}
