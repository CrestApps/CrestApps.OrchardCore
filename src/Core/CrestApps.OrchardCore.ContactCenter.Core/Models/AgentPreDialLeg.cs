namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// An agent leg rung while its offer was still ringing, and how far it has come towards being joined to the caller.
/// </summary>
/// <remarks>
/// The leg is joined to the caller only once both halves are true: the offer was accepted and the caller's leg is
/// ready (<see cref="CallerReadyUtc"/>), and the agent's device answered the leg (<see cref="AgentAnsweredUtc"/>).
/// They arrive in either order -- the agent's client answers the leg the moment they click, in parallel with the
/// accept -- so whichever arrives second joins them, exactly once (<see cref="BridgedUtc"/>).
/// </remarks>
public sealed class AgentPreDialLeg
{
    /// <summary>
    /// Gets or sets the offer (reservation) the leg was rung for.
    /// </summary>
    public string ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the agent profile the offer was presented to.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user the agent profile represents.
    /// </summary>
    public string AgentUserId { get; set; }

    /// <summary>
    /// Gets or sets the interaction the offer is for.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider that rang the leg.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the caller's leg the agent leg is joined to.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the agent leg.
    /// </summary>
    public string AgentLegId { get; set; }

    /// <summary>
    /// Gets or sets when the leg was rung.
    /// </summary>
    public DateTime DialedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the leg stops ringing on its own if nobody answers it.
    /// </summary>
    public DateTime RingsUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets when the agent's device answered the leg.
    /// </summary>
    public DateTime? AgentAnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets when the offer was accepted and the caller's leg made ready to be joined.
    /// </summary>
    public DateTime? CallerReadyUtc { get; set; }

    /// <summary>
    /// Gets or sets when the leg was joined to the caller. Once set the leg is simply the agent's leg of the call.
    /// </summary>
    public DateTime? BridgedUtc { get; set; }
}
