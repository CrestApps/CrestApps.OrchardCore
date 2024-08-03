namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class Invoice
{
    public InvoiceLineItem[] LineItems { get; set; }

    public double GetDueNow()
        => LineItems
        .Where(x => x.DueNow.HasValue)
        .Sum(x => x.DueNow.Value);

    public Dictionary<BillingDurationKey, double> GetSubtotals()
        => LineItems.GroupBy(x => new BillingDurationKey(x.DurationType, x.BillingDuration))
        .ToDictionary(x => x.Key, x => x.Sum(y => y.Subtotal));

    public double GetTotal()
        => GetDueNow() + GetSubtotals().Sum(x => x.Value);
}
