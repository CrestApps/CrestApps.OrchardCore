namespace CrestApps.OrchardCore.Asterisk.Web.Models;

/// <summary>
/// Represents the Asterisk resources created for a two-party call simulation.
/// </summary>
public sealed class TwoPartyCallSimulationResult
{
    /// <summary>
    /// Gets or sets the simulation correlation identifier.
    /// </summary>
    public string SimulationId { get; set; }

    /// <summary>
    /// Gets or sets the mixing bridge identifier.
    /// </summary>
    public string BridgeId { get; set; }

    /// <summary>
    /// Gets or sets the first party channel identifier.
    /// </summary>
    public string PartyAChannelId { get; set; }

    /// <summary>
    /// Gets or sets the second party channel identifier.
    /// </summary>
    public string PartyBChannelId { get; set; }
}
