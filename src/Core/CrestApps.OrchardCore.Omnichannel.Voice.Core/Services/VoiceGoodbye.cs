namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Recognises a closing line, so that what the assistant says after asking to end a call is muted only when it is a
/// goodbye said twice.
/// </summary>
/// <remarks>
/// <para>
/// A model that has said its goodbye, calls the end-call tool and reads the tool's reply often says the same goodbye
/// again, and the repeat is muted. But a model also calls the tool early -- "let me just read that back", then the
/// tool, then the read-back and the goodbye -- and muting everything after the call cut off what the customer was
/// waiting to hear. So the line before has to have been a goodbye for the next to count as a repeat.
/// </para>
/// <para>
/// English phrases only. A line in another language is never taken for a goodbye, which at worst lets a repeated
/// goodbye be heard -- the older, lesser problem -- and never mutes what the assistant still had to say.
/// </para>
/// </remarks>
internal static class VoiceGoodbye
{
    private static readonly string[] _closings =
    [
        "bye",
        "take care",
        "talk soon",
        "talk to you soon",
        "talk to you later",
        "speak soon",
        "have a great",
        "have a good",
        "have a nice",
        "have a wonderful",
        "have a lovely",
        "enjoy your day",
        "enjoy the rest of",
        "all the best",
        "thanks for your time",
        "thank you for your time",
    ];

    /// <summary>
    /// Whether the line is a goodbye.
    /// </summary>
    /// <param name="line">The assistant's line, as transcribed.</param>
    public static bool SoundsLikeOne(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        foreach (var closing in _closings)
        {
            if (line.Contains(closing, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
