namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// The order an agent checkout placed.
/// </summary>
public sealed class AgentOrderReference
{
    /// <summary>
    /// Gets or sets the order's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the order's number, which the buyer sees.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the address of a page the buyer follows the order on.
    /// </summary>
    public string PermalinkUrl { get; set; }
}
