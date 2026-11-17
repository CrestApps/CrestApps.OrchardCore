namespace CrestApps.OrchardCore.Payroll.Core.Canada;

/// <summary>
/// The Canadian social insurance number: nine digits whose last is a check digit (the Luhn mod-10 check), so a typo is
/// caught when it is entered and before a T4 file holds it.
/// </summary>
public static class CanadaSin
{
    /// <summary>
    /// Gets whether a number is a valid social insurance number: nine digits (spaces and dashes ignored) that pass the
    /// mod-10 check, such as 046 454 286.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => !char.IsAsciiDigit(character) && character is not (' ' or '-')))
        {
            return false;
        }

        var digits = value.Where(char.IsAsciiDigit).Select(character => character - '0').ToArray();

        if (digits.Length != 9 || digits.All(digit => digit == 0))
        {
            return false;
        }

        var sum = 0;

        for (var i = 0; i < digits.Length; i++)
        {
            // Every second digit is doubled, and a two-digit product's digits are added.
            var digit = i % 2 == 1 ? digits[i] * 2 : digits[i];

            sum += digit > 9 ? digit - 9 : digit;
        }

        return sum % 10 == 0;
    }
}
