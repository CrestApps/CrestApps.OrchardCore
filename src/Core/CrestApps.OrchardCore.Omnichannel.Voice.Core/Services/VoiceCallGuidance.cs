using CrestApps.OrchardCore.Omnichannel.Voice.Tools;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Guidance every automated call gives the model, whichever way the call is being held.
/// </summary>
/// <remarks>
/// A live speech-to-speech session and the turn-based speak-and-transcribe loop are two ways of running the same
/// conversation, so what the model is told about the call itself belongs in one place. Keeping it here is what
/// stops the two drifting apart -- which they had: only the realtime session was ever told it could end the call,
/// so a turn-based call said goodbye and then sat there until the customer gave up and hung up.
/// </remarks>
internal static class VoiceCallGuidance
{
    /// <summary>
    /// Tells the model that hanging up is its job, and when to do it.
    /// </summary>
    /// <remarks>
    /// Said plainly, because the model is speaking rather than writing and cannot see the call state: on a phone
    /// call somebody has to hang up, and if it does not, the customer is left holding a dead line.
    /// </remarks>
    public const string EndingTheCall =
        "You are on a live phone call. When the conversation has genuinely finished — the customer has what " +
        "they needed, has declined, has asked not to be called again, or has said goodbye — say a short, warm " +
        "closing line and then call the " + EndCallTool.ToolName + " tool. The call is hung up for you once you " +
        "have finished speaking and the customer has had a moment to add anything, so do not announce that you " +
        "are hanging up and do not wait for them to do it. Never call it while the customer still has questions " +
        "or is being transferred to a person.";

    /// <summary>
    /// The same guidance under its own heading, for a system prompt that is assembled in sections.
    /// </summary>
    public const string EndingTheCallSection = "## Ending the call\n\n" + EndingTheCall;
}
