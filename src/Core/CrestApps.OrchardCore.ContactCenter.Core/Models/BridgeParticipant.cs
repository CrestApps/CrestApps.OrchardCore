namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents one party's membership of a bridge, bounded by the time it joined and the time it left.
/// Membership is append-only: a party that leaves keeps its record with <see cref="LeftUtc"/> set, so
/// who was present at any past instant can be reconstructed rather than inferred from a live count.
/// </summary>
public sealed class BridgeParticipant
{
    /// <summary>
    /// Gets or sets the provider identifier of the participating leg.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the part this participant plays in the call.
    /// </summary>
    public CallPartyRole Role { get; set; }

    /// <summary>
    /// Gets or sets the agent identifier when the participant is an agent or a supervisor.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the address of the participating party.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the participant joined the bridge.
    /// </summary>
    public DateTime JoinedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the participant left the bridge, or <see langword="null"/> while it is
    /// still present.
    /// </summary>
    public DateTime? LeftUtc { get; set; }
}
