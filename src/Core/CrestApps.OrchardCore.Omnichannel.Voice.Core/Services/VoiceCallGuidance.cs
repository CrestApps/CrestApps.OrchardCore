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

    /// <summary>
    /// The heading <see cref="WhenTalkedOver"/> is given in a live session's instructions.
    /// </summary>
    public const string WhenTalkedOverHeading = "## When the customer talks over you";

    /// <summary>
    /// Tells the model what a caller who talks over it has and has not heard, and what to do about it.
    /// </summary>
    /// <remarks>
    /// When the caller talks over the assistant its line is cut back to what they heard, so the model's own record
    /// of an interrupted opening is a word or two long. Asked to open by introducing itself, it then has no choice
    /// but to start the whole introduction again. Live, a caller who said "hello?" as they picked up was greeted
    /// from the top four times in fifteen seconds and never got to say why they were still on the line.
    /// </remarks>
    public const string WhenTalkedOver =
        "If the customer speaks while you are talking, you stop, and they heard only what you had said up to " +
        "that moment. Answer what they actually said. If they cut in during your opening, do not start your " +
        "introduction again from the beginning: reply to them in a few words and carry on from where you were " +
        "cut off. Never say your full opening line more than once on a call. A \"hello?\" said over you usually " +
        "means they had not heard you yet, so a quick hello and your name is enough before you ask your question.";
}
