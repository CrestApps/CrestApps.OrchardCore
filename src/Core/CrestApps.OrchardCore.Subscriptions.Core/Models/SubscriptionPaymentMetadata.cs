namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class SubscriptionPaymentMetadata
{
    public string PlanId { get; set; }

    public double? Amount { get; set; }

    public string Currency { get; set; }
}
