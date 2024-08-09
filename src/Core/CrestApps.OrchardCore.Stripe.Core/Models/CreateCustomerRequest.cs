namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateCustomerRequest
{
    public string PaymentMethodId { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    public Dictionary<string, string> Metadata { get; set; }
}
