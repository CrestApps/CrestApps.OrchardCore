namespace CrestApps.OrchardCore.Subscriptions.Models;

public class CreateSessionSubscriptionPayment
{
    public string CustomerId { get; set; }

    public string PaymentMethodId { get; set; }

    public string SessionId { get; set; }

    public Dictionary<string, string> Metadata { get; set; }
}
