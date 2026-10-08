namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// How an automated AI voice conversation ended.
/// </summary>
public enum AIVoiceSessionOutcome
{
    /// <summary>
    /// The end was not observed.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The assistant finished the conversation and ended the call itself.
    /// </summary>
    CompletedByAI = 1,

    /// <summary>
    /// The assistant handed the caller to a live agent.
    /// </summary>
    HandedToAgent = 2,

    /// <summary>
    /// The call reached a voicemail rather than a person.
    /// </summary>
    Voicemail = 3,

    /// <summary>
    /// Nobody answered the call.
    /// </summary>
    NoAnswer = 4,

    /// <summary>
    /// The caller hung up before the assistant ended the conversation.
    /// </summary>
    CallerHungUp = 5,

    /// <summary>
    /// The conversation failed: the call ended in a failed state, or the live session reported an error.
    /// </summary>
    Failed = 6,
}
