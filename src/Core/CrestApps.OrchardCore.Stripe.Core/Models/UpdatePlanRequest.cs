namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class UpdatePlanRequest
{
    public string Title { get; set; }

    public string ProductId { get; set; }

    public bool? IsActive { get; set; }
}
