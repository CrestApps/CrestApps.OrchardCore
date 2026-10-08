namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Recognises an assistant line that leaves the customer something to answer: a question, or the announcement of
/// a read-back they will be asked to confirm.
/// </summary>
/// <remarks>
/// <para>
/// A model that has decided a call is over can talk straight through the confirmation it set up. Live, one said
/// "let me read that back to make sure I caught it", ended the call in the same breath, and then read the email
/// address back, asked "did I get that right?" and thanked the customer for a "yes" they never gave. Asking the
/// model not to, in the tool's description, was not enough; so a line like that keeps the call open until the
/// customer has answered.
/// </para>
/// <para>
/// English phrases for the announcement; a question mark in any language. A line in another language that
/// announces a read-back without asking anything is not caught, which leaves the call as it was before this.
/// </para>
/// </remarks>
internal static class VoiceConfirmation
{
    private static readonly string[] _announcements =
    [
        "read that back",
        "read it back",
        "read this back",
        "read back",
        "let me confirm",
        "let me double-check",
        "let me double check",
        "let me make sure",
        "let me repeat",
        "just to confirm",
        "just to make sure",
    ];

    /// <summary>
    /// Whether the line leaves the customer something to answer.
    /// </summary>
    /// <param name="line">The assistant's line, as transcribed.</param>
    public static bool AwaitsAnswer(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (line.TrimEnd(' ', '"', '\'', '”', '’', ')').EndsWith('?'))
        {
            return true;
        }

        foreach (var announcement in _announcements)
        {
            if (line.Contains(announcement, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
