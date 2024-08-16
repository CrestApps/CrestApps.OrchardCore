namespace CrestApps.OrchardCore.Payments.Models;

public class PaymentMethodOptions
{
    public string DefaultPaymentMethod { get; set; }

    public IList<PaymentMethod> PaymentMethods { get; } = [];
}
