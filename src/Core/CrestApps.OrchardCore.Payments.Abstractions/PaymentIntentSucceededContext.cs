namespace CrestApps.OrchardCore.Payments;

public sealed class PaymentIntentSucceededContext : PaymentEventContextBase
{
    public double? Amount { get; set; }

    public string Currency { get; set; }

    public string TransactionId { get; set; }
}
