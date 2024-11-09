namespace CrestApps.OrchardCore.Payments.Models;

public sealed class PaymentMethodOptions
{
    public string DefaultPaymentMethod { get; set; }

    public Dictionary<string, PaymentMethod> PaymentMethods { get; } = [];
}
