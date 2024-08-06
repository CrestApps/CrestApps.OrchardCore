namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateProductRequest
{
    public string Id { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Valid values 'good', 'service', or 'planet'
    /// </summary>
    public string Type { get; set; }
}
