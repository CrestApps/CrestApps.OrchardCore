namespace CrestApps.OrchardCore.Payments;

public sealed class PaymentSucceededContext : PaymentEventContextBase
{
    public double AmountPaid { get; set; }

    public string Currency { get; set; }
}
