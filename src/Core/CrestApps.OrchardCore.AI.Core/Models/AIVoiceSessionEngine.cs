namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// The conversation engine that held an automated AI voice call.
/// </summary>
public enum AIVoiceSessionEngine
{
    /// <summary>
    /// Transcribe, complete, synthesize: the assistant answers once the caller has finished and been transcribed.
    /// </summary>
    TurnBased = 0,

    /// <summary>
    /// A live speech-to-speech session: caller audio goes to the model as it arrives, and its voice comes back.
    /// </summary>
    Realtime = 1,
}
