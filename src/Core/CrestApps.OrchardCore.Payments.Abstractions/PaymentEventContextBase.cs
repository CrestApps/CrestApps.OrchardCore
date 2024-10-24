namespace CrestApps.OrchardCore.Payments;

public class PaymentEventContextBase
{
    public string GatewayId { get; set; }

    public GatewayMode GatewayMode { get; set; }

    public Dictionary<string, object> Data { get; } = new(StringComparer.OrdinalIgnoreCase);
}
