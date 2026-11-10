using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;

/// <summary>
/// Finds agent checkouts by platform, store checkout, order and expiry.
/// </summary>
public sealed class AgentCheckoutIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the agent platform that made it.
    /// </summary>
    public string PlatformId { get; set; }

    /// <summary>
    /// Gets or sets the protocol it was made through.
    /// </summary>
    public string Protocol { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it can't change any more.
    /// </summary>
    public bool IsTerminal { get; set; }

    /// <summary>
    /// Gets or sets the store checkout that holds it.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the order of the store checkout.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets when it expires.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }
}
