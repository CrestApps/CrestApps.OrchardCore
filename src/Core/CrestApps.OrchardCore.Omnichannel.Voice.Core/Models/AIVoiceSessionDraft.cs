using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// What the conversation loop knows about a call when the assistant's part of it ends, handed over to be written
/// as an <see cref="AIVoiceSessionSummary"/>.
/// </summary>
/// <remarks>
/// Plain values only: it is written in a scope of its own, after the request the call ran in may already be gone.
/// Everything durable -- the activity, the transcript, the names -- is read again where it is written.
/// </remarks>
public sealed class AIVoiceSessionDraft
{
    /// <summary>
    /// Gets or sets the activity the call belongs to.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the provider that carried the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the call.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the engine that held the call.
    /// </summary>
    public AIVoiceSessionEngine Engine { get; set; }

    /// <summary>
    /// Gets or sets the deployment the call was held on, when the engine resolved one itself. When empty, the
    /// deployment the profile's chat slot resolves to is recorded.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets how the conversation ended, when the engine knows. When empty, it is worked out from the
    /// activity and the transcript.
    /// </summary>
    public AIVoiceSessionOutcome? Outcome { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call was answered.
    /// </summary>
    public bool WasAnswered { get; set; }

    /// <summary>
    /// Gets or sets when the assistant's part of the call ended, used when nothing was measured.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets what was measured on the call's audio, or <see langword="null"/> when this node never saw it.
    /// </summary>
    public AIVoiceSessionMeasurements Measurements { get; set; }
}
