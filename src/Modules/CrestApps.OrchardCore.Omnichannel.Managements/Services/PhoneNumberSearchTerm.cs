using System.Text;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal readonly record struct PhoneNumberSearchTerm
{
    /// <summary>
    /// The country calling code a national search is read in when it carries none.
    /// </summary>
    /// <remarks>
    /// The search box has no country beside it, and the numbers these lists hold are overwhelmingly North American,
    /// so a ten-digit entry is read as a NANP number. It only ever widens an exact search: the digits as typed are
    /// still compared with the national columns, so a number from another country is found the way it always was.
    /// </remarks>
    internal const string DefaultCountryCallingCode = "1";

    private const int _nanpNationalNumberLength = 10;

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

    /// <summary>
    /// Gets every E.164 value an exact search for this term may mean.
    /// </summary>
    /// <remarks>
    /// An E.164 entry means itself. A national entry is compared with the national columns as typed, but those only
    /// hold what the import or the editor happened to write there, so the same number is also looked for in the
    /// E.164 columns: a ten-digit entry in the <see cref="DefaultCountryCallingCode"/> country, and a longer entry as
    /// one that already carries its country code (for example <c>15555550101</c>). The values are only checked for
    /// the E.164 shape, not against the numbering plan, because a search has to find whatever was stored, including
    /// numbers the plan does not recognise.
    /// </remarks>
    public IReadOnlyList<string> GetExactE164Candidates()
    {
        if (IsE164)
        {
            return [Value];
        }

        var candidate = Value.Length == _nanpNationalNumberLength
            ? $"+{DefaultCountryCallingCode}{Value}"
            : Value.Length > _nanpNationalNumberLength
                ? $"+{Value}"
                : null;

        return candidate is not null && PhoneNumber.IsE164(candidate)
            ? [candidate]
            : [];
    }

    /// <summary>
    /// Gets the national-column values an exact search should also match on a record whose number could not be
    /// turned into E.164.
    /// </summary>
    /// <remarks>
    /// A number imported without a country (for example <c>15555550101</c>) cannot be canonicalised, so its E.164
    /// column stays empty and its national column keeps whatever digits arrived, country code included or not. The
    /// E.164 candidates cannot reach such a record, so the same number is looked for in the shapes it was most
    /// likely written in. These are only compared where the E.164 column is empty: a record that was canonicalised is
    /// matched on its E.164 value instead, which is what keeps a foreign number whose national digits happen to look
    /// like one of these shapes from being found by mistake.
    /// </remarks>
    public IReadOnlyList<string> GetExactUncanonicalNationalCandidates()
    {
        var candidates = new List<string>(2);
        var digits = Value;

        if (IsE164)
        {
            // The digits of an E.164 entry are the number written with its country code and without the plus.
            digits = Value[1..];
            candidates.Add(digits);
        }
        else if (digits.Length == _nanpNationalNumberLength)
        {
            // A national entry, stored with the country code in front of it.
            candidates.Add($"{DefaultCountryCallingCode}{digits}");
        }

        if (digits.Length == _nanpNationalNumberLength + DefaultCountryCallingCode.Length &&
            digits.StartsWith(DefaultCountryCallingCode, StringComparison.Ordinal))
        {
            // An entry that carries the country code, stored without it.
            candidates.Add(digits[DefaultCountryCallingCode.Length..]);
        }

        return candidates;
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
