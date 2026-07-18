namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the provider-facing outcome of routing an inbound voice event into Contact Center work.
/// </summary>
public sealed class InboundVoiceRouteOutcome
{
    /// <summary>
    /// Gets or sets the identifier of the durable Contact Center interaction created or resolved for the inbound
    /// call, or <see langword="null"/> when routing did not produce a durable interaction.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets a value indicating whether routing produced a durable Contact Center interaction a provider may promote
    /// its caller leg against.
    /// </summary>
    public bool HasInteraction => !string.IsNullOrEmpty(InteractionId);
}
