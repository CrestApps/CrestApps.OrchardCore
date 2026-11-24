using CrestApps.OrchardCore.Numbering;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Shows and reads certificate verification codes: stored as 16 characters of <see cref="ReadableCodes.Alphabet"/>,
/// shown in groups of four.
/// </summary>
public static class CertificateVerificationCodes
{
    /// <summary>
    /// Shows a code in groups of four, such as <c>ABCD-EFGH-JKLM-NPQR</c>.
    /// </summary>
    /// <param name="code">The stored code.</param>
    /// <returns>The code to show, or <see langword="null"/>.</returns>
    public static string Format(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        return string.Join('-', Enumerable.Range(0, (code.Length + 3) / 4).Select(index => code.Substring(index * 4, Math.Min(4, code.Length - (index * 4)))));
    }

    /// <summary>
    /// Turns a code as a person types it into the stored form: the letters and digits of the code alphabet, upper case.
    /// </summary>
    /// <param name="code">The typed code.</param>
    /// <returns>The code, or <see langword="null"/> when it cannot be one.</returns>
    public static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var characters = code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray();

        return characters.Length == SkillsConstants.VerificationCodeLength && characters.All(character => ReadableCodes.Alphabet.Contains(character))
            ? new string(characters)
            : null;
    }
}
