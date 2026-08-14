using System.Text;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal readonly record struct PhoneNumberSearchTerm
{
    private PhoneNumberSearchTerm(string value, bool isE164)
    {
        Value = value;
        IsE164 = isE164;
    }

    public string Value { get; }

    public bool IsE164 { get; }

    public static bool TryParse(string input, out PhoneNumberSearchTerm searchTerm)
    {
        searchTerm = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        var digits = NormalizeDigits(trimmed);

        if (string.IsNullOrEmpty(digits))
        {
            return false;
        }

        var isE164 = trimmed[0] == '+';
        searchTerm = new PhoneNumberSearchTerm(isE164 ? $"+{digits}" : digits, isE164);

        return true;
    }

    public string GetPattern(PhoneNumberMatchType matchType)
    {
        return matchType switch
        {
            PhoneNumberMatchType.Exact => Value,
            PhoneNumberMatchType.BeginsWith => $"{Value}%",
            PhoneNumberMatchType.EndsWith => $"%{Value}",
            PhoneNumberMatchType.Contains => $"%{Value}%",
            _ => throw new ArgumentOutOfRangeException(nameof(matchType), matchType, "Unsupported phone number match type."),
        };
    }

    internal static string NormalizeDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
