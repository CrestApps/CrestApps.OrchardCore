namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class UpdateCustomerResponse
{
    public bool Updated { get; set; }

    public string CustomerId { get; set; }

    public string Name { get; set; }

    public string Phone { get; set; }

    public string Email { get; set; }
}
