namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// A link an agent shows the buyer, such as the store's terms of service.
/// </summary>
public sealed class AgentLink
{
    /// <summary>
    /// Gets or sets the kind of link, one of <see cref="AgentLinkTypes"/>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the absolute address.
    /// </summary>
    public string Url { get; set; }
}
