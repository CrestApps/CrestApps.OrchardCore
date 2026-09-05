namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// The four moments in a call the automated conversation reacts to. A provider translates its own event names
/// into these, so the loop never learns any provider's vocabulary.
/// </summary>
public enum VoiceAgentEventKind
{
    /// <summary>
    /// Anything the loop does not act on.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The person picked up.
    /// </summary>
    Answered,

    /// <summary>
    /// The assistant finished speaking.
    /// </summary>
    SpeechEnded,

    /// <summary>
    /// A transcript of what the person said arrived.
    /// </summary>
    Transcription,

    /// <summary>
    /// The call ended.
    /// </summary>
    Hangup,
}
