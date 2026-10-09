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
