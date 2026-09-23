using System.Text;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Recognises the recorded greeting of a voicemail or answering machine in what a call transcribed.
/// </summary>
/// <remarks>
/// A turn-based call hears the line only as text, and a voicemail greeting arrives exactly like somebody answering.
/// Live, "when you have finished recording you may hang up" was taken as the customer's reply: the assistant said
/// goodbye to it, and the review read the call as the customer declining and concluded it as do-not-call, opting out
/// somebody who had never spoken. The phrases below are what these systems say and what a person picking up does
/// not, and they are only consulted before the customer has said anything, where a greeting is what is expected.
/// </remarks>
public static class VoicemailGreeting
{
    // What identifies a recording rather than a person.
    private static readonly string[] _greetingPhrases =
    [
        "leave a message",
        "leave your message",
        "leave me a message",
        "leave a voicemail",
        "leave a brief message",
        "leave your name",
        "record your message",
        "finished recording",
        "you may hang up",
        "after the tone",
        "after the beep",
        "at the tone",
        "at the beep",
        "voicemail",
        "voice mail",
        "voice messaging",
        "mailbox",
        "the person you are calling",
        "the person you're calling",
        "the number you have dialed",
        "the number you have reached",
        "you have reached",
        "you've reached",
        "subscriber you have called",
        "customer you are calling",
        "can't come to the phone",
        "cannot come to the phone",
        "can't take your call",
        "cannot take your call",
        "unable to take your call",
        "get back to you as soon as",
        "return your call",
        "press pound",
    ];

    // What a greeting says as it hands over to the recording, so the message can be left now rather than after
    // waiting for the line to go quiet.
    private static readonly string[] _invitationPhrases =
    [
        "leave a message",
        "leave your message",
        "leave me a message",
        "leave a voicemail",
        "leave a brief message",
        "leave your name",
        "record your message",
        "finished recording",
        "you may hang up",
        "after the tone",
        "after the beep",
        "at the tone",
        "at the beep",
    ];

    /// <summary>
    /// Whether the text reads as a voicemail or answering machine greeting.
    /// </summary>
    /// <param name="text">A transcribed turn.</param>
    public static bool IsRecordedGreeting(string text)
        => ContainsAny(text, _greetingPhrases);

    /// <summary>
    /// Whether the greeting has reached the point where it asks for the message.
    /// </summary>
    /// <param name="text">A transcribed turn already recognised as a greeting.</param>
    public static bool InvitesTheMessage(string text)
        => ContainsAny(text, _invitationPhrases);

    /// <summary>
    /// Whether the other side of this transcript was a recording from its first word.
    /// </summary>
    /// <remarks>
    /// Read from the stored transcript rather than from any flag, so a call concluded after the session holding it
    /// has gone -- and a call held by a live session that records no such flag -- is judged the same way.
    /// </remarks>
    /// <param name="prompts">The stored transcript for the call's session.</param>
    public static bool OpensWithRecordedGreeting(IEnumerable<AIChatSessionPrompt> prompts)
    {
        var firstCallerTurn = prompts?
            .FirstOrDefault(prompt => prompt is not null &&
                prompt.Role == ChatRole.User &&
                !prompt.IsGeneratedPrompt &&
                !string.IsNullOrWhiteSpace(prompt.Content));

        return firstCallerTurn is not null && IsRecordedGreeting(firstCallerTurn.Content);
    }

    /// <summary>
    /// Whether a concluded call was answered by voicemail rather than by the customer.
    /// </summary>
    /// <remarks>
    /// A mark the provider made on its own word is not the final say: detection can mistake a person for a
    /// machine, and a call that went on to hold a conversation -- the customer answering more than once in
    /// words no greeting uses -- is the conversation it was. Every other mark, and a transcript that opens with a
    /// recorded greeting, is taken as it stands.
    /// </remarks>
    /// <param name="voicemail">The mark on the activity, if any.</param>
    /// <param name="prompts">The stored transcript for the call's session.</param>
    public static bool ReachedVoicemail(VoicemailReached voicemail, IEnumerable<AIChatSessionPrompt> prompts)
    {
        var turns = prompts?.Where(prompt => prompt is not null).ToList() ?? [];

        if (OpensWithRecordedGreeting(turns))
        {
            return true;
        }

        if (voicemail is null)
        {
            return false;
        }

        if (!voicemail.DetectedByProvider || voicemail.MessageLeft)
        {
            return true;
        }

        var conversationalTurns = turns.Count(prompt =>
            prompt.Role == ChatRole.User &&
            !prompt.IsGeneratedPrompt &&
            !string.IsNullOrWhiteSpace(prompt.Content) &&
            !IsRecordedGreeting(prompt.Content));

        return conversationalTurns < 2;
    }

    private static bool ContainsAny(string text, string[] phrases)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = Normalize(text);

        foreach (var phrase in phrases)
        {
            if (normalized.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Lower case, curly apostrophes made straight, and punctuation and runs of spaces collapsed to single spaces, so
    // "Please leave a message, after the tone." matches however the transcription punctuated it.
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = true;

        foreach (var character in text)
        {
            var current = character is '\u2019' or '\u2018' ? '\'' : char.ToLowerInvariant(character);

            if (char.IsLetterOrDigit(current) || current == '\'')
            {
                builder.Append(current);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
