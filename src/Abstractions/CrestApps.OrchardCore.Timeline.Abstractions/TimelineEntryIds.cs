using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Makes the id of an entry brought in from somewhere else from where it came from, so importing the same thing twice
/// finds the entry already stored instead of adding it again.
/// </summary>
public static class TimelineEntryIds
{
    private const int Length = 26;
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>
    /// Returns the id for an entry from its source, such as the record and the old note's id.
    /// </summary>
    /// <param name="parts">What identifies the source; the same parts always give the same id.</param>
    /// <returns>A 26-character id.</returns>
    public static string FromSource(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', parts.Select(part => part ?? string.Empty))));
        var id = new StringBuilder(Length);

        for (var index = 0; index < Length; index++)
        {
            id.Append(Alphabet[hash[index] % Alphabet.Length]);
        }

        return id.ToString();
    }
}
