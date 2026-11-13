namespace CrestApps.OrchardCore.Banking.Core.Services;

/// <summary>
/// The number checks every country shares.
/// </summary>
public static class BankNumberFormat
{
    /// <summary>
    /// The longest account number any country uses: an IBAN has at most 34 characters.
    /// </summary>
    public const int MaxLength = 34;

    /// <summary>
    /// Removes the spaces, dashes and dots people type between groups of digits, and writes letters in upper case.
    /// </summary>
    /// <param name="value">The number as entered.</param>
    /// <returns>The number without separators, or an empty string.</returns>
    public static string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(character => !char.IsWhiteSpace(character) && character is not '-' and not '.')
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    /// <summary>
    /// Checks that a cleaned number has only letters A to Z and digits, and a length between two bounds.
    /// </summary>
    /// <param name="value">The cleaned number.</param>
    /// <param name="minLength">The shortest length allowed.</param>
    /// <param name="maxLength">The longest length allowed.</param>
    /// <param name="digitsOnly">Whether only digits are allowed.</param>
    /// <returns><see langword="true"/> when the number has that form.</returns>
    public static bool HasForm(string value, int minLength, int maxLength, bool digitsOnly)
    {
        if (string.IsNullOrEmpty(value) || value.Length < minLength || value.Length > maxLength)
        {
            return false;
        }

        return value.All(character => char.IsAsciiDigit(character) || (!digitsOnly && char.IsAsciiLetterUpper(character)));
    }
}
