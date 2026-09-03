namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// A durable marker stored on the automated voice activity's property bag when the model invokes the
/// transfer-to-agent tool. It survives across Telnyx webhooks (the tool fires during the transcription turn, but
/// the call is bridged only after the closing line finishes on the next speak.ended event), replacing the old
/// in-transcript text marker.
/// </summary>
public sealed class PendingVoiceHandoff
{
    /// <summary>
    /// Gets or sets the reason the model gave for escalating.
    /// </summary>
    public string Reason { get; set; }
}
