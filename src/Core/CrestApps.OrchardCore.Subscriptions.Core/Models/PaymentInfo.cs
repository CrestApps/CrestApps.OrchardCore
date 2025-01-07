using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// This class is used in session to track payment info.
/// </summary>
public class PaymentInfo
{
    public PaymentStatus Status { get; set; }

    public double Amount { get; set; }

    public string Currency { get; set; }

    public string SubscriptionId { get; set; }

    public string GatewayId { get; set; }

    public GatewayMode GatewayMode { get; set; }

    public string TransactionId { get; set; }
}

public enum PaymentStatus
{
    Unknown,
    Succeeded,
    Failed,
}
