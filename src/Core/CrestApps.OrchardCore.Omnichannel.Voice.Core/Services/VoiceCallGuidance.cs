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
    /// <para>
    /// The confirmation rule is here because ending the call is where a wrong one costs: live, a model read an
    /// email address back wrongly, the customer's reply came through garbled, and the model took it for a yes,
    /// thanked them and hung up with the wrong address.
    /// </para>
    /// <para>
    /// The callback rule is here for the same reason: live, a customer said "can you call me later?" and the model
    /// said goodbye and hung up without asking when, so the follow-up could only be scheduled for a guess. And a
    /// customer who answered "no, not right now" was wished a good day and closed as finished -- never called again
    /// -- when "not now" is a timing answer, not a refusal.
    /// </para>
    /// <para>
    /// The closing line is held to one sentence because every word of it is waited through: live, a confirmed email
    /// was followed by "let me just wrap this up with you" and a two-sentence goodbye, twelve seconds of talking
    /// before the line could drop.
    /// </para>
    /// </remarks>
    public const string EndingTheCall =
        "You are on a live phone call. When the conversation has genuinely finished — the customer has what " +
        "they needed, has declined, has asked not to be called again, or has said goodbye — say one short, warm " +
        "closing sentence (thank them and say goodbye) and then call the " + EndCallTool.ToolName + " tool. Do " +
        "not say you are wrapping up or about to finish first; just say goodbye. The call is hung up for you once you " +
        "have finished speaking and the customer has had a moment to add anything, so do not announce that you " +
        "are hanging up and do not wait for them to do it. Never call it while the customer still has questions " +
        "or is being transferred to a person. When you read details back to confirm them, only a clear yes " +
        "confirms them: an answer you could not make out, or one that does not plainly say yes, is not a " +
        "confirmation -- ask again (\"Sorry, was that a yes?\"), and if they correct you, read the corrected " +
        "details back before you go on. If the customer says now is not a good time, offer to call them back " +
        "unless they have made clear they are not interested at all. If they ask to be called back, or accept " +
        "the offer, without saying when, ask once when would suit them before you close, and say that time back " +
        "to them in your goodbye.";

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
    /// <para>
    /// It used to state as a fact that the opening "has been said", from the first word of the call. A model that
    /// takes its instructions literally believed it: it opened one call with "You still with me?", and on the next,
    /// when the caller's "hello?" cancelled its greeting before a word of it played, it answered "Yes, I'm here"
    /// and never introduced itself. So the opening is described as something that happens once, and a "hello?"
    /// before it is the cue to give it.
    /// </para>
    /// </remarks>
    public const string WhenTalkedOver =
        "Your first line on this call is your opening: greet the customer and introduce yourself as your " +
        "instructions describe. If you have not spoken yet, give it even if the customer speaks first — a " +
        "\"hello?\" before you have said anything is your cue to open, not a sign they have been waiting. Once your " +
        "opening has been said, the customer knows who you are and why you are calling, even if they talked over " +
        "part of it. Any instructions about how to open the call apply only to your first line. Never say it " +
        "again: do not greet the customer by name again, and do not say who you are or where you are calling from " +
        "again unless they ask. " +
        "If the customer speaks while you are talking, you stop; answer what they actually said. After your " +
        "opening, if they say \"hello?\", \"are you there?\" or \"can you hear me?\", answer in a few words " +
        "(\"Yes, I'm here!\") and go straight on with the question you were asking. If they only acknowledge you " +
        "(\"OK\", \"yeah\"), carry on with the conversation rather than starting it again.";
}
