namespace CrestApps.OrchardCore.Payments;

public class PaymentEventContextBase
{
    public GatewayMode Mode { get; set; }

    public Dictionary<string, object> Data { get; } = new(StringComparer.OrdinalIgnoreCase);
}
