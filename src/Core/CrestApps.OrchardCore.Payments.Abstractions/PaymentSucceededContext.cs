namespace CrestApps.OrchardCore.Payments;

public sealed class PaymentSucceededContext : PaymentEventContextBase
{
    public double AmountPaid { get; set; }

    public string Currency { get; set; }

    public string TransactionId { get; set; }

    public SubscriptionPaymentInfo Subscription { get; set; }

    public PaymentReason Reason { get; set; }

}
