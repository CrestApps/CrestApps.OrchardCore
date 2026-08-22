using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents one live call leg belonging to a call session. A leg is a single party's connection; the
/// session's <see cref="Bridge"/> describes which legs currently hear one another.
/// </summary>
public sealed class CallLeg
{
    /// <summary>
    /// Gets or sets the provider identifier of the leg.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the part this leg's party plays in the call.
    /// </summary>
    public CallPartyRole Role { get; set; }

    /// <summary>
    /// Gets or sets the normalized lifecycle state of the leg.
    /// </summary>
    public CallLegStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the address of the party on the leg.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the agent identifier when the leg belongs to an agent or a supervisor.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the leg was created.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the leg was answered.
    /// </summary>
    public DateTime? AnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the leg ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral reason the leg ended.
    /// </summary>
    public HangupCause? HangupCause { get; set; }
}
