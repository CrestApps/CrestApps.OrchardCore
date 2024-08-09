namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class PlanResponse
{
    public string Id { get; set; }

    public string ProductId { get; set; }

    public string Title { get; set; }

    public bool IsActive { get; set; }
}
