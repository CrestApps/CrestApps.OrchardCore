namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Filters a page of agent checkouts.
/// </summary>
public sealed class AgentCheckoutQuery
{
    /// <summary>
    /// Gets or sets the platform the checkouts came from, or <see langword="null"/> for every platform.
    /// </summary>
    public string PlatformId { get; set; }

    /// <summary>
    /// Gets or sets whether the checkouts ended (completed, canceled or expired), are still open, or either.
    /// </summary>
    public bool? Ended { get; set; }

    /// <summary>
    /// Gets or sets whether the checkouts became an order, did not, or either.
    /// </summary>
    public bool? HasOrder { get; set; }
}
