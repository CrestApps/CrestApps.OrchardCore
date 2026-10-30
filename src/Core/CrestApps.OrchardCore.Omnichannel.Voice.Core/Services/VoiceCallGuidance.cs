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
    /// thanked them and hung up with the wrong address. It is limited to what the customer gave: live, a model that
    /// had given the customer a phone number asked them four times whether it was right. That model had also made
    /// the number up, hence the rule against inventing details. Actions are held to the same rule: live, a profile with
    /// no booking tool said it would confirm availability and get back to the customer, went silent, and then told
    /// them the appointment was booked. Asked not to, a profile whose instructions said "you book appointments" still
    /// said one was set, so the rule says it holds over the profile's own description of its job.
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
        "or is being transferred to a person. When you read back details the customer gave you to confirm them, " +
        "only a clear yes confirms them: an answer you could not make out, or one that does not plainly say yes, " +
        "is not a confirmation -- ask again (\"Sorry, was that a yes?\"), and if they correct you, read the " +
        "corrected details back before you go on. Information you give the customer needs no confirmation: say it " +
        "once, and repeat it only if they ask. Never make up a phone number, address, price or other detail you " +
        "were not given; if you do not have it, say so. Never tell the customer something is booked, confirmed or " +
        "done unless one of your tools did it, and never say you will check something and come back to them unless a " +
        "tool is doing it now: if you cannot do it on this call, say what you have noted and who will follow up. " +
        "This holds even when your instructions say booking, scheduling or confirming is your job: without a tool " +
        "that does it, you take the request down and someone confirms it later. " +
        "If the customer says now is not a good time, offer to call them back " +
        "unless they have made clear they are not interested at all. If they ask to be called back, or accept " +
        "the offer, without saying when, ask once when would suit them before you close, and say that time back " +
        "to them in your goodbye.";

    /// <summary>
    /// How to get past a phone menu or a phone's call screening on a call the AI placed.
    /// </summary>
    /// <remarks>
    /// A business often answers with a menu, and a phone may screen an unknown caller by asking who they are and why
    /// they are calling before it rings. Neither is the customer, and neither is a voicemail.
    /// </remarks>
    public const string PhoneMenusAndScreening =
        "If an automated phone menu answers, listen to the options, then use the " + PressKeysTool.ToolName + " tool " +
        "to press the key that gets you to the person or department you need, or to enter an extension you were " +
        "given, and wait for what the menu says next. Press only that key, once: not also the keys for hearing the menu " +
        "again or going back. When a person answers after the menu, greet them as you would anybody who answers. If " +
        "the phone asks who is calling and why before it connects you (call screening), say your name, the business " +
        "you are calling for and the reason for the call in one " +
        "short sentence, then wait: the person will usually pick up next.";

    /// <summary>
    /// The same guidance under its own heading, for a system prompt that is assembled in sections.
    /// </summary>
    public const string EndingTheCallSection = "## Ending the call\n\n" + EndingTheCall;

    /// <summary>
    /// What the model is told when the call was answered by voicemail or an answering machine and the time has come
    /// to leave the message.
    /// </summary>
    /// <remarks>
    /// Shared by both ways of holding a call, so the two leave the same kind of message. A turn-based call puts it to
    /// the model as the turn it answers; a live session asks for one response under its own instructions, so the
    /// message comes in the persona, voice and language the call has been held in -- which is why it is worded for
    /// whatever language that is, and never names one. It forbids questions outright: a question left on a recording
    /// is never answered, and is the surest sign the model carried on the conversation instead of leaving a message.
    /// Live, it asked the voicemail whether the customer wanted a new or used vehicle.
    /// </remarks>
    public const string LeavingAVoicemail =
        "This call went to voicemail and it is now recording; anything the customer seemed to say before this was " +
        "its recorded greeting, not the customer. Say only the voicemail message to leave, in one to three short " +
        "sentences: greet the customer by name if you know it, say who you are and why you called, and say you will " +
        "try them again. Leave it in the language you have been speaking on this call. It is a recording, so do not " +
        "ask any questions, do not wait for a reply, and say nothing else.";

    /// <summary>
    /// The heading <see cref="RecordingDisclosure(string)"/> is given in a live session's instructions.
    /// </summary>
    public const string RecordingDisclosureHeading = "## Telling the customer the call is recorded";

    /// <summary>
    /// Tells a live session to give the tenant's recording disclosure word for word, before anything else.
    /// </summary>
    /// <remarks>
    /// A live session writes its own opening line, so the platform cannot say the disclosure for it the way the
    /// turn-based loop does. A legal notice has to be the approved words, so the model is told to quote it rather
    /// than to mention that the call is recorded, and to say it again in full if the caller talked over it.
    /// </remarks>
    /// <param name="disclosure">The disclosure text, exactly as the tenant configured it.</param>
    /// <returns>The guidance, or <see langword="null"/> when there is no disclosure to give.</returns>
    public static string RecordingDisclosure(string disclosure)
    {
        if (string.IsNullOrWhiteSpace(disclosure))
        {
            return null;
        }

        return
            "This call is recorded, and the customer must be told so before anything else. Your opening must begin " +
            "with this sentence, said word for word exactly as it is written here: \"" + disclosure.Trim() + "\" " +
            "Do not paraphrase, shorten, translate or skip it, and do not add anything to it; then go straight on " +
            "with the rest of your opening. Only if you were cut off before you finished that sentence, start your " +
            "next reply with the whole sentence again. Once you have said the whole sentence, never say it again, " +
            "even if the customer spoke while you were saying it or while you went on; if they ask whether the call " +
            "is recorded, simply tell them that it is.";
    }

    /// <summary>
    /// Tells the model the name of the person it is calling, and to greet them by their first name.
    /// </summary>
    /// <remarks>
    /// A contact's name is stored whole, and a model given it greets with all of it: live, "Hi Mike Demo". And a model
    /// given no name does not go without one, it invents one.
    /// </remarks>
    /// <param name="contactName">The contact's name, or <see langword="null"/> when it is not known.</param>
    /// <returns>The guidance.</returns>
    public static string WhoYouAreCalling(string contactName)
    {
        var name = contactName?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return "You do not know the name of the person you are calling. Do not use a name, and never guess or " +
                "invent one; ask who you are speaking with if you need it.";
        }

        var firstName = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

        return $"You are calling {name}. That is the only name you may use for them. Never use any other name, and " +
            "never guess or invent one — if the person says they are somebody else, believe them and adjust. " +
            (firstName == name
                ? $"Greet them as {name}."
                : $"Greet them and speak to them by their first name, {firstName}, never by their full name, unless " +
                  "you need to check you are speaking to the right person.");
    }

    /// <summary>
    /// What a live session's opening response is asked for when the call is recorded: the disclosure first, word for
    /// word, then the rest of the opening.
    /// </summary>
    /// <param name="disclosure">The disclosure text, exactly as the tenant configured it.</param>
    /// <returns>The instruction, or <see langword="null"/> when there is no disclosure to give.</returns>
    public static string OpeningWithDisclosure(string disclosure)
    {
        if (string.IsNullOrWhiteSpace(disclosure))
        {
            return null;
        }

        return
            "Give your opening line now. Its first words must be exactly these, with nothing before them and nothing " +
            "changed: \"" + disclosure.Trim() + "\" Then, in the same breath, greet the customer and introduce " +
            "yourself as your instructions describe.";
    }

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
