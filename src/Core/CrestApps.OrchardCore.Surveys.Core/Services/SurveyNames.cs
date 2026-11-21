using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Builds technical names, variable codes and random tokens.
/// </summary>
public static class SurveyNames
{
    private const string TokenAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    /// <summary>
    /// Turns a title into a technical name of lowercase letters, digits and dashes.
    /// </summary>
    /// <param name="text">The title.</param>
    /// <returns>The name, or <c>survey</c> when nothing is left.</returns>
    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "survey";
        }

        var normalized = text.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var dash = false;

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);

            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(lower);
                dash = false;
            }
            else if (!dash && builder.Length > 0)
            {
                builder.Append('-');
                dash = true;
            }
        }

        var result = builder.ToString().Trim('-');

        if (result.Length > 80)
        {
            result = result.Substring(0, 80).Trim('-');
        }

        return result.Length == 0 ? "survey" : result;
    }

    /// <summary>
    /// Gets whether a technical name holds only lowercase letters, digits and dashes.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it is valid.</returns>
    public static bool IsValidName(string name)
        => !string.IsNullOrEmpty(name)
            && name.Length <= 100
            && name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    /// <summary>
    /// Gets whether a variable code is valid: a letter or an underscore, then letters, digits and underscores.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when it is valid.</returns>
    public static bool IsValidVariableCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > 100)
        {
            return false;
        }

        if (!(char.IsAsciiLetter(code[0]) || code[0] == '_'))
        {
            return false;
        }

        return code.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    }

    /// <summary>
    /// Makes a random URL-safe token.
    /// </summary>
    /// <param name="length">The number of characters.</param>
    /// <returns>The token.</returns>
    public static string NewToken(int length = 22)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];

        for (var i = 0; i < length; i++)
        {
            chars[i] = TokenAlphabet[bytes[i] & 63];
        }

        return new string(chars);
    }

    /// <summary>
    /// Makes a random secret of 32 bytes, written in base64.
    /// </summary>
    /// <returns>The secret.</returns>
    public static string NewSecret()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
