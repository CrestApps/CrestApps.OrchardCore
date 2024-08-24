namespace CrestApps.OrchardCore.Payments;

public sealed class PaymentIntentSucceededContext : PaymentEventContextBase
{
    public double? AmountPaid { get; set; }

    public string Currency { get; set; }
}
