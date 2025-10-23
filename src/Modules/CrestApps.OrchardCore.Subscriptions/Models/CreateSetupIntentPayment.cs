namespace CrestApps.OrchardCore.Subscriptions.Models;

public class CreateSetupIntentPayment
{
    public string SessionId { get; set; }

    public string PaymentMethodId { get; set; }

    public Dictionary<string, string> Metadata { get; set; }
}
