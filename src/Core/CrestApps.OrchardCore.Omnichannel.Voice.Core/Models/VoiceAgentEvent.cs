namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// A call event in the only terms the automated conversation needs: what happened, on which call, for which
/// activity, and what the person said.
/// </summary>
public sealed class VoiceAgentEvent
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public VoiceAgentEventKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the person's leg.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the telephony provider carrying the call, so the loop resolves the
    /// media that can act on <see cref="ProviderCallId"/>.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the omnichannel activity this call belongs to.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets what the person said, on a <see cref="VoiceAgentEventKind.Transcription"/> event.
    /// </summary>
    public string TranscriptionText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the transcript is the final one for the utterance. Interim
    /// transcripts are ignored: answering half a sentence talks over the person saying the rest of it.
    /// </summary>
    public bool TranscriptionIsFinal { get; set; }
}
