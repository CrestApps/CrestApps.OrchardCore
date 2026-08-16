namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents content type settings for a subscription part.
/// </summary>
public class SubscriptionPartSettings
{
    /// <summary>
    /// Gets or sets the content types for which the subscription flow should collect content item data.
    /// </summary>
    public string[] ContentTypes { get; set; }
}
