using System.Text.RegularExpressions;
using CrestApps.Core.AI.Memory;

namespace CrestApps.Core.AI.Services;

public sealed partial class DefaultAIMemorySafetyService : IAIMemorySafetyService
{
    public bool TryValidate(string name, string description, string content, out string errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "Memory name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errorMessage = "Memory description is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            errorMessage = "Memory content is required.";
            return false;
        }

        if (LooksSensitive(content) || LooksSensitive(name) || LooksSensitive(description))
        {
            errorMessage = "Sensitive information must not be stored in user memory.";
            return false;
        }

        return true;
    }

    private static bool LooksSensitive(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (SensitiveKeywordRegex().IsMatch(value) || SocialSecurityRegex().IsMatch(value))
        {
            return true;
        }

        foreach (Match match in CandidateCardRegex().Matches(value))
        {
            var digits = DigitsOnlyRegex().Replace(match.Value, string.Empty);

            if (digits.Length >= 13 && digits.Length <= 19 && PassesLuhn(digits))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var shouldDouble = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';

            if (shouldDouble)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            shouldDouble = !shouldDouble;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"\b(?:password|api[\s_-]?key|secret|access[\s_-]?token|refresh[\s_-]?token|private[\s_-]?key|connection[\s_-]?string|credit[\s_-]?card|ssn|social[\s_-]?security)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveKeywordRegex();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex SocialSecurityRegex();

    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CandidateCardRegex();

    [GeneratedRegex(@"\D", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsOnlyRegex();
}
