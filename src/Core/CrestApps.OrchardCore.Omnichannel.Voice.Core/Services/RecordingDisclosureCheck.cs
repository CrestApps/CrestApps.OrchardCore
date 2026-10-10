using System.Text;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Decides whether an assistant's opening line gave the tenant's recording disclosure word for word.
/// </summary>
/// <remarks>
/// A realtime session writes its own opening, so being told to quote the disclosure is not proof that it did. The
/// line is compared word for word, ignoring only case and punctuation, which speech transcription does not keep
/// reliably: a paraphrase, a shortened notice or a missing word is not the notice the tenant approved.
/// </remarks>
internal static class RecordingDisclosureCheck
{
    /// <summary>
    /// Finds the line of the assistant's opening that gave the disclosure, following the opening the way a call
    /// actually goes: a person who says "hello?" over the first words cuts the line off partway through the
    /// disclosure, and the assistant then says it again in full. An attempt cut off partway through is passed over;
    /// the first line that contains the whole disclosure gave it; a line that goes on to anything else without it
    /// means the assistant moved on without giving it.
    /// </summary>
    /// <param name="disclosure">The disclosure the assistant was to give.</param>
    /// <param name="assistantLines">What the assistant said, in order.</param>
    /// <returns>
    /// Whether the disclosure was given; the line that gave it, or else the line that moved on without it (or the
    /// last attempt, or <see langword="null"/> when the assistant never spoke).
    /// </returns>
    public static (bool Said, string Line) FindInOpening(string disclosure, IEnumerable<string> assistantLines)
    {
        var expected = Normalize(disclosure);
        string lastAttempt = null;

        if (expected.Length == 0 || assistantLines is null)
        {
            return (false, null);
        }

        foreach (var line in assistantLines)
        {
            var spoken = Normalize(line);

            if (spoken.Length == 0)
            {
                continue;
            }

            if (WasSaid(disclosure, line))
            {
                return (true, line);
            }

            // Cut off partway through the disclosure: the next line is where it is said again.
            if (expected.StartsWith(spoken, StringComparison.Ordinal))
            {
                lastAttempt = line;

                continue;
            }

            return (false, line);
        }

        return (false, lastAttempt);
    }

    /// <summary>
    /// Returns whether the opening line contains the whole disclosure, word for word.
    /// </summary>
    /// <param name="disclosure">The disclosure the assistant was to give.</param>
    /// <param name="openingLine">What the assistant actually said first.</param>
    public static bool WasSaid(string disclosure, string openingLine)
    {
        var expected = Normalize(disclosure);

        if (expected.Length == 0 || string.IsNullOrWhiteSpace(openingLine))
        {
            return false;
        }

        // Padded with spaces so a disclosure ending in "purposes" is not matched by "purposesless".
        return (" " + Normalize(openingLine) + " ").Contains(" " + expected + " ", StringComparison.Ordinal);
    }

    /// <summary>
    /// Reduces text to its lower-case words separated by single spaces.
    /// </summary>
    /// <param name="text">The text to reduce.</param>
    internal static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var character in text)
        {
            // An apostrophe joins a word ("you're"), so it is dropped rather than turned into a break.
            if (character is '\'' or '’')
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }
}
