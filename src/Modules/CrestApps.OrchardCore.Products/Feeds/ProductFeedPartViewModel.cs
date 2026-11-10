namespace CrestApps.OrchardCore.Products.Feeds;

public class ProductFeedPartViewModel
{
    public string Brand { get; set; }

    public string Gtin { get; set; }

    public string Mpn { get; set; }

    public string Condition { get; set; }

    public string GoogleProductCategory { get; set; }

    public bool ExcludeFromFeeds { get; set; }

    /// <summary>
    /// Gets or sets <c>yes</c>, <c>no</c> or empty for the store's default.
    /// </summary>
    public string ShowToAgents { get; set; }

    /// <summary>
    /// Gets or sets <c>yes</c>, <c>no</c> or empty for the store's default.
    /// </summary>
    public string AgentCheckout { get; set; }
}
