namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreatePlanRequest
{
    public string ProductId { get; set; }

    public string Id { get; set; }

    public string Title { get; set; }

    public double? Amount { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Valid values are month, year, week, or day
    /// </summary>
    public string Interval { get; set; }

    public int? IntervalCount { get; set; }
}
