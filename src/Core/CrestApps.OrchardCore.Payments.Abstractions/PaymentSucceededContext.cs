namespace CrestApps.OrchardCore.Payments;

public sealed class PaymentSucceededContext : PaymentEventContextBase
{
    public double AmountPaid { get; set; }

    public string Currency { get; set; }

    public string TransactionId { get; set; }

    public GatewayMode Mode { get; set; }

    public SubscriptionPaymentInfo Subscription { get; set; }

    public PaymentReason Reason { get; set; }
}

public sealed class SubscriptionPaymentInfo : PaymentEventContextBase
{
    public string SubscriptionId { get; set; }
}

public enum PaymentReason
{
    Manual,
    SubscriptionCreate,
    SubscriptionCycle,
    SubscriptionUpdate,
    Other,
}
