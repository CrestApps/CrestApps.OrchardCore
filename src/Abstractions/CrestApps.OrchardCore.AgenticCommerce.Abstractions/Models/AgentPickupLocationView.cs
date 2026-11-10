using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// A place to collect an agent checkout's order from.
/// </summary>
public sealed class AgentPickupLocationView
{
    /// <summary>
    /// Gets or sets the location's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets its name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it is.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets the fee for collecting there.
    /// </summary>
    public decimal Fee { get; set; }
}
