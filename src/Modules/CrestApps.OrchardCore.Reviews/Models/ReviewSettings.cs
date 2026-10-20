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

    /// <summary>
    /// Gets or sets whether buyers who accept marketing are emailed, some days after paying, to review what they bought.
    /// </summary>
    public bool SendReviewRequests { get; set; }

    /// <summary>
    /// Gets or sets the days after an order is paid its review request goes, such as 14 for time to use what came.
    /// </summary>
    public int ReviewRequestDelayDays { get; set; } = 14;
}
