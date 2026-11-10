using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// How an agent checkout's order reaches the buyer.
/// </summary>
public sealed class AgentFulfillmentView
{
    /// <summary>
    /// Gets or sets a value indicating whether the order is shipped or collected at all.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets where it ships.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets the ways to ship to the address, cheapest first.
    /// </summary>
    public IList<AgentFulfillmentOptionView> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets the chosen way to ship.
    /// </summary>
    public string SelectedOptionId { get; set; }

    /// <summary>
    /// Gets or sets the places to collect from.
    /// </summary>
    public IList<AgentPickupLocationView> PickupLocations { get; set; } = [];

    /// <summary>
    /// Gets or sets the chosen place to collect from.
    /// </summary>
    public string SelectedPickupLocationId { get; set; }
}
