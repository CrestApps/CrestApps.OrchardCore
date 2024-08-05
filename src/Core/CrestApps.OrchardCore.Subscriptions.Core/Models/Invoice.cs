namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class Invoice
{
    public double DueNow { get; set; }

    public double GrandTotal { get; set; }

    public Dictionary<BillingDurationKey, double> Subtotals { get; set; }

    public InvoiceLineItem[] LineItems { get; set; }
}
