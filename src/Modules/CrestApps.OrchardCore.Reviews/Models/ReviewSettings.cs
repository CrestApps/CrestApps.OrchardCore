namespace CrestApps.OrchardCore.Reviews.Models;

public sealed class ReviewSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether a review waits for the store to approve it before it shows.
    /// </summary>
    public bool RequireApproval { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether only buyers who paid for the product can review it.
    /// </summary>
    public bool BuyersOnly { get; set; } = true;
}
