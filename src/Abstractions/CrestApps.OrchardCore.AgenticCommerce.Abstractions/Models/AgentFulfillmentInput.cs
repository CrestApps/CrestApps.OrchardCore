using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// How the agent wants the order: shipped to an address, or collected.
/// </summary>
public sealed class AgentFulfillmentInput
{
    /// <summary>
    /// Gets or sets where to ship.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets the chosen way to ship, or <see langword="null"/> for the cheapest.
    /// </summary>
    public string OptionId { get; set; }

    /// <summary>
    /// Gets or sets the place to collect from, instead of shipping.
    /// </summary>
    public string PickupLocationId { get; set; }
}
