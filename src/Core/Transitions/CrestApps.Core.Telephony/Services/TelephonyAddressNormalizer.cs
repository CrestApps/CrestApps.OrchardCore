using System.Text;

namespace CrestApps.Core.Telephony.Services;

/// <summary>
/// Reduces a dialable address to the characters that identify it, so two spellings of the same number
/// compare equal.
/// </summary>
/// <remarks>
/// This is a comparison aid, not a parser: it keeps digits and a leading plus and drops everything else,
/// without consulting a numbering plan. Anything that has to know whether a number is valid, what region
/// it belongs to, or how to display it goes through the phone-number service instead.
/// </remarks>
public static class TelephonyAddressNormalizer
{
    /// <summary>
    /// Normalizes a dialable address for comparison.
    /// </summary>
    /// <param name="value">The address as it was written.</param>
    /// <returns>
    /// The digits, with a leading plus preserved, or <see langword="null"/> when nothing identifying is left.
    /// </returns>
    public static string NormalizePhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);

        foreach (var character in trimmed)
        {
            if (char.IsDigit(character) || character == '+' && builder.Length == 0)
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
