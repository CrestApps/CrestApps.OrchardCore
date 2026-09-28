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
    /// <para>
    /// A caller who talks over the assistant stops hearing it. Cutting that line back to what they heard deletes
    /// the provider's text of it, so the opening is no longer cut back at all (see
    /// <see cref="AssistantBargeIn.IsOpeningLine(string)"/>): its words are the model's record of having
    /// introduced itself.
    /// </para>
    /// <para>
    /// The first version of this said "do not start your introduction again", and on the next live call the model
    /// still greeted the caller from the top four times in fifteen seconds — including in answer to "are you
    /// there?". The profile's own opening instructions ("let them speak first, then open") read, on every
    /// "hello?", like the cue to open. So this now says outright that those instructions are spent after the first
    /// line, and what to say instead.
    /// </para>
    /// </remarks>
    public const string WhenTalkedOver =
        "Your first line on this call was your opening, and it has been said: from then on the customer knows who " +
        "you are and why you are calling, even if they talked over part of it. Any instructions about how to open " +
        "the call apply only to your first line. Never say it again: do not greet the customer by name again, and " +
        "do not say who you are or where you are calling from again unless they ask. " +
        "If the customer speaks while you are talking, you stop; answer what they actually said. If they say " +
        "\"hello?\", \"are you there?\" or \"can you hear me?\", answer in a few words (\"Yes, I'm here!\") and go " +
        "straight on with the question you were asking. If they only acknowledge you (\"OK\", \"yeah\"), carry on " +
        "with the conversation rather than starting it again.";
}
