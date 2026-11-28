using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Checks the free text that AI tools moving money or credit take (a payment reference, a note, a reason) for a payment
/// card number or a bank account number (IBAN), which is never given to the assistant nor kept by it (the owner's review,
/// F23). A run of 13 to 19 digits counts only when it passes the Luhn check card numbers carry, and an IBAN only when it
/// is written in capitals and passes its mod-97 check, so ordinary references such as <c>FY25 payment for March</c> or
/// <c>Wire ref 2025-11-20-0001</c> are accepted. Domestic bank details (routing, sort code or account numbers) carry no
/// check digits, so they count when bank wording is followed right away by a run of six or more digits.
/// </summary>
public static partial class AIMoneyArguments
{
    private const int MinCardDigits = 13;
    private const int MaxCardDigits = 19;
    private const int MinIbanLength = 15;
    private const int MaxIbanLength = 34;
    private const int MinBankDigits = 6;

    /// <summary>
    /// Refuses texts that hold what looks like a card or bank account number.
    /// </summary>
    /// <param name="refused">How a refusal starts, such as "Nothing was recorded".</param>
    /// <param name="texts">The texts, such as the reference, the note and the reason; blanks are ignored.</param>
    /// <returns>The sentence that says why the text is refused, or <see langword="null"/>.</returns>
    public static string RefuseAccountNumbers(string refused, params string[] texts)
    {
        foreach (var text in texts ?? [])
        {
            if (ContainsAccountNumber(text))
            {
                return $"{refused}: the text looks like a card or bank account number. Never give one; use a check number or a short transfer reference instead.";
            }
        }

        return null;
    }

    /// <summary>
    /// Returns whether a text holds what looks like a card number or an IBAN.
    /// </summary>
    /// <param name="text">The text.</param>
    public static bool ContainsAccountNumber(string text)
        => !string.IsNullOrWhiteSpace(text) && (ContainsCardNumber(text) || ContainsIban(text) || ContainsBankDetails(text));

    /// <summary>
    /// Returns whether a text holds domestic bank details, which carry no check digits of their own: bank wording (a
    /// routing, ABA, transit, sort code or BSB number, an account number) followed right away (after at most a colon,
    /// a number sign, "no", "number" or "is") by a run of at least six digits. A short reference such as
    /// <c>account ending 4321</c>, a check number, or bank wording followed by other words is accepted.
    /// </summary>
    /// <param name="text">The text.</param>
    public static bool ContainsBankDetails(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (Match match in BankDetails().Matches(text))
        {
            var digits = 0;

            foreach (var character in match.Groups["digits"].Value)
            {
                if (char.IsAsciiDigit(character))
                {
                    digits++;
                }
            }

            if (digits >= MinBankDigits)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether a text holds a run of 13 to 19 digits, written together or in groups split by single spaces or
    /// dashes, that passes the Luhn check.
    /// </summary>
    /// <param name="text">The text.</param>
    public static bool ContainsCardNumber(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (Match run in DigitRun().Matches(text))
        {
            var groups = run.Value.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);

            // Every stretch of whole groups is a candidate, so a card number next to another number is still found.
            for (var start = 0; start < groups.Length; start++)
            {
                var digits = new StringBuilder();

                for (var end = start; end < groups.Length; end++)
                {
                    digits.Append(groups[end]);

                    if (digits.Length > MaxCardDigits)
                    {
                        break;
                    }

                    if (digits.Length >= MinCardDigits && IsValidLuhn(digits.ToString()))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether a text holds an IBAN: a country code in capitals, two check digits and 11 to 30 capitals or digits,
    /// written together or in groups split by single spaces, that pass the mod-97 check.
    /// </summary>
    /// <param name="text">The text.</param>
    public static bool ContainsIban(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (Match start in IbanStart().Matches(text))
        {
            var compact = new StringBuilder(start.Value);
            var index = start.Index + start.Length;

            while (index < text.Length && compact.Length < MaxIbanLength)
            {
                var character = text[index];

                if (character == ' ' && index + 1 < text.Length && IsIbanCharacter(text[index + 1]))
                {
                    index++;

                    continue;
                }

                if (!IsIbanCharacter(character))
                {
                    break;
                }

                compact.Append(character);
                index++;

                // A candidate ends where a group ends: at the end of the text, a space or anything else.
                var next = index < text.Length ? text[index] : ' ';

                if (!IsIbanCharacter(next) && compact.Length >= MinIbanLength && IsValidIban(compact.ToString()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether digits pass the Luhn check payment card numbers carry.
    /// </summary>
    /// <param name="digits">The digits, nothing else.</param>
    public static bool IsValidLuhn(string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return false;
        }

        var sum = 0;
        var alternate = false;

        for (var index = digits.Length - 1; index >= 0; index--)
        {
            var current = digits[index] - '0';

            if (current is < 0 or > 9)
            {
                return false;
            }

            if (alternate)
            {
                current *= 2;

                if (current > 9)
                {
                    current -= 9;
                }
            }

            sum += current;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// Returns whether a compact IBAN (no spaces) passes its mod-97 check: the first four characters moved to the end,
    /// letters turned into numbers (A is 10), and the remainder of the division by 97 is 1.
    /// </summary>
    /// <param name="iban">The IBAN, in capitals, without spaces.</param>
    public static bool IsValidIban(string iban)
    {
        if (string.IsNullOrEmpty(iban) || iban.Length is < MinIbanLength or > MaxIbanLength ||
            !char.IsAsciiLetterUpper(iban[0]) || !char.IsAsciiLetterUpper(iban[1]) ||
            !char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3]))
        {
            return false;
        }

        var remainder = 0;

        foreach (var character in iban[4..] + iban[..4])
        {
            if (char.IsAsciiDigit(character))
            {
                remainder = ((remainder * 10) + (character - '0')) % 97;
            }
            else if (char.IsAsciiLetterUpper(character))
            {
                remainder = ((remainder * 100) + (character - 'A' + 10)) % 97;
            }
            else
            {
                return false;
            }
        }

        return remainder == 1;
    }

    private static bool IsIbanCharacter(char character)
        => char.IsAsciiDigit(character) || char.IsAsciiLetterUpper(character);

    [GeneratedRegex("[0-9](?:[ -]?[0-9])*", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRun();

    [GeneratedRegex(@"\b[A-Z]{2}[0-9]{2}", RegexOptions.CultureInvariant)]
    private static partial Regex IbanStart();

    [GeneratedRegex(@"(?:\b(?:routing|aba|bsb|acct|a/c|sort\s*code|transit\s+(?:no|number)|account\s*(?:no|number|num)|bank\s+account)\b|\baccount\s*#)[\s:#.\-]{0,4}(?:(?:no|number|num|is)\b[\s:#.\-]{0,4})?(?<digits>[0-9](?:[ \-]?[0-9])*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BankDetails();
}
