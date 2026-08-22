namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// This class is used in the cached payment session.
/// </summary>
public class SubscriptionPaymentsMetadata
{
    public Dictionary<string, PaymentInfo> Payments { get; set; }
}
