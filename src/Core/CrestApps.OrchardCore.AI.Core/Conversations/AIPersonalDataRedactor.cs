using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// A personal detail found in a conversation, to be replaced wherever it appears.
/// </summary>
public sealed class AIPersonalDataTerm
{
    /// <summary>
    /// Gets or sets the exact text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the kind: <c>name</c>, <c>address</c>, <c>birthdate</c> or <c>id</c>.
    /// </summary>
    public string Kind { get; set; }
}

/// <summary>
/// Replaces personal details in text with placeholders: email addresses, phone numbers, payment card numbers and
/// social security numbers by pattern, and names, addresses and other details the utility model found by the exact
/// text it found.
/// </summary>
public static partial class AIPersonalDataRedactor
{
    /// <summary>
    /// Replaces an email address.
    /// </summary>
    public const string EmailPlaceholder = "[EMAIL]";

    /// <summary>
    /// Replaces a phone number.
    /// </summary>
    public const string PhonePlaceholder = "[PHONE]";

    /// <summary>
    /// Replaces a payment card number.
    /// </summary>
    public const string CardPlaceholder = "[CARD]";

    /// <summary>
    /// Replaces an identity, account or member number.
    /// </summary>
    public const string IdPlaceholder = "[ID NUMBER]";

    /// <summary>
    /// Replaces a person's name.
    /// </summary>
    public const string NamePlaceholder = "[NAME]";

    /// <summary>
    /// Replaces a street address.
    /// </summary>
    public const string AddressPlaceholder = "[ADDRESS]";

    /// <summary>
    /// Replaces a date of birth.
    /// </summary>
    public const string BirthDatePlaceholder = "[DATE OF BIRTH]";

    /// <summary>
    /// Replaces the personal details in a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="terms">The details the utility model found, if any.</param>
    public static string Redact(string text, IEnumerable<AIPersonalDataTerm> terms = null)
        => RedactTerms(RedactPatterns(text), terms);

    /// <summary>
    /// Replaces the details recognized by pattern: email addresses, payment card numbers (checked with the Luhn
    /// algorithm), US social security numbers, and phone numbers of ten to fifteen digits, or eight or more after
    /// a <c>+</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    public static string RedactPatterns(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = EmailPattern().Replace(text, EmailPlaceholder);
        text = CardPattern().Replace(text, match => IsCardNumber(match.Value) ? CardPlaceholder : match.Value);
        text = SocialSecurityPattern().Replace(text, IdPlaceholder);

        return PhonePattern().Replace(text, match => IsPhoneNumber(match.Value) ? PhonePlaceholder : match.Value);
    }

    /// <summary>
    /// Replaces each found detail wherever it appears, ignoring case, longest first so a full name goes before the
    /// first name inside it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="terms">The details.</param>
    public static string RedactTerms(string text, IEnumerable<AIPersonalDataTerm> terms)
    {
        if (string.IsNullOrEmpty(text) || terms is null)
        {
            return text;
        }

        foreach (var term in terms
            .Where(term => term?.Text?.Trim().Length >= 2)
            .OrderByDescending(term => term.Text.Trim().Length))
        {
            // Whole words only, so a name like "Al" does not take the letters out of "also".
            var pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(term.Text.Trim()) + @"(?![\p{L}\p{N}])";
            text = Regex.Replace(text, pattern, GetPlaceholder(term.Kind), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }

        return text;
    }

    /// <summary>
    /// Gets the placeholder for a kind of detail.
    /// </summary>
    /// <param name="kind">The kind.</param>
    public static string GetPlaceholder(string kind)
        => kind?.Trim().ToLowerInvariant() switch
        {
            "name" => NamePlaceholder,
            "address" => AddressPlaceholder,
            "birthdate" => BirthDatePlaceholder,
            "email" => EmailPlaceholder,
            "phone" => PhonePlaceholder,
            _ => IdPlaceholder,
        };

    internal static bool IsCardNumber(string value)
    {
        var digits = value.Where(char.IsAsciiDigit).Select(digit => digit - '0').ToArray();

        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;

        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i];

            if (i % 2 == 1)
            {
                digit *= 2;

                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }

    internal static bool IsPhoneNumber(string value)
    {
        if (DatePattern().IsMatch(value.Trim()))
        {
            return false;
        }

        var digits = value.Count(char.IsAsciiDigit);

        return value.TrimStart().StartsWith('+')
            ? digits is >= 8 and <= 15
            : digits is >= 10 and <= 15;
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<![\d-])(?:\d[ -]?){12,18}\d(?![\d-])", RegexOptions.CultureInvariant)]
    private static partial Regex CardPattern();

    [GeneratedRegex(@"(?<!\d)\d{3}-\d{2}-\d{4}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex SocialSecurityPattern();

    [GeneratedRegex(@"(?<![\w+])\+?\(?\d[\d ().-]{6,}\d(?!\w)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^\d{4}-\d{1,2}-\d{1,2}$|^\d{1,2}[./-]\d{1,2}[./-]\d{2,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
}
