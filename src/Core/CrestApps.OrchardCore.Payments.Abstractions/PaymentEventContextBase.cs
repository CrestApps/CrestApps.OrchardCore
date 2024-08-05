namespace CrestApps.OrchardCore.Payments;

public class PaymentEventContextBase
{
    public Dictionary<string, object> Data { get; } = new(StringComparer.OrdinalIgnoreCase);
}
