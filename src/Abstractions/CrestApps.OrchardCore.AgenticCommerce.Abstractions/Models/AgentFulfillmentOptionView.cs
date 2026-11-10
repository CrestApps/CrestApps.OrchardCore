namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// A way to ship an agent checkout's order.
/// </summary>
public sealed class AgentFulfillmentOptionView
{
    /// <summary>
    /// Gets or sets the option's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the option's name.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets its description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the carrier.
    /// </summary>
    public string Carrier { get; set; }

    /// <summary>
    /// Gets or sets what it costs, before tax.
    /// </summary>
    public decimal Amount { get; set; }
}
