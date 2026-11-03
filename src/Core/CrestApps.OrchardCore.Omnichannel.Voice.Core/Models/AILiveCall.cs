namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// The provider call an automated voice activity's AI is talking on, kept on the activity from the moment the call
/// is answered so a supervisor can watch it, message the AI, take it over or end it.
/// </summary>
public sealed class AILiveCall
{
    /// <summary>
    /// Gets or sets the telephony provider carrying the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier of the customer's call leg.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets when the call was answered.
    /// </summary>
    public DateTime AnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a live (realtime) session holds the call, rather than the turn-based
    /// loop.
    /// </summary>
    public bool IsRealtime { get; set; }
}
