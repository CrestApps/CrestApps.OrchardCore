namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a provider call leg recorded as part of an interaction's communication history.
/// </summary>
public sealed class InteractionCallLeg
{
    /// <summary>
    /// Gets or sets the provider leg identifier.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets the source address for the leg.
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the destination address for the leg.
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the leg started.
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
    /// Gets or sets the part this leg's party played in the call.
    /// </summary>
    public CallPartyRole Role { get; set; }

    /// <summary>
    /// Gets or sets the normalized lifecycle state of the leg.
    /// </summary>
    public CallLegStatus Status { get; set; }
}
