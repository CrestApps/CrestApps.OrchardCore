using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Banking.Checks.Services;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// Writes amounts in English words for the legal line of a check: "One thousand two hundred thirty-four and 56/100".
/// </summary>
public sealed class EnglishAmountInWords : IAmountInWords
{
    private static readonly string[] _ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];

    private static readonly string[] _tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    private static readonly (decimal Value, string Name)[] _scales =
    [
        (1_000_000_000_000m, "trillion"),
        (1_000_000_000m, "billion"),
        (1_000_000m, "million"),
        (1_000m, "thousand"),
    ];

    private static readonly Dictionary<string, string> _currencyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = "Dollars",
        ["CAD"] = "Dollars",
        ["AUD"] = "Dollars",
        ["NZD"] = "Dollars",
        ["EUR"] = "Euros",
        ["GBP"] = "Pounds",
        ["MXN"] = "Pesos",
        ["JPY"] = "Yen",
        ["CHF"] = "Francs",
    };

    /// <inheritdoc />
    public string Language => "en";

    /// <inheritdoc />
    public string Write(decimal amount, int decimals = 2, string currency = null)
    {
        var words = WriteNumber(amount, decimals);

        return string.IsNullOrEmpty(currency)
            ? words
            : words + " " + _currencyNames.GetValueOrDefault(currency, currency.ToUpperInvariant());
    }

    private static string WriteNumber(decimal amount, int decimals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);

        var rounded = Math.Round(amount, decimals, MidpointRounding.AwayFromZero);
        var whole = decimal.Truncate(rounded);
        var words = Words(whole);
        var text = char.ToUpperInvariant(words[0]) + words.Substring(1);

        if (decimals == 0)
        {
            return text;
        }

        var denominator = (long)Math.Pow(10, decimals);
        var fraction = (long)((rounded - whole) * denominator);

        return text + " and " + fraction.ToString(new string('0', decimals), CultureInfo.InvariantCulture) + "/" + denominator.ToString(CultureInfo.InvariantCulture);
    }

    private static string Words(decimal number)
    {
        if (number == 0)
        {
            return _ones[0];
        }

        var builder = new StringBuilder();

        foreach (var (value, name) in _scales)
        {
            if (number >= value)
            {
                var count = decimal.Truncate(number / value);

                Append(builder, BelowThousand((int)count) + " " + name);
                number -= count * value;
            }
        }

        if (number > 0)
        {
            Append(builder, BelowThousand((int)number));
        }

        return builder.ToString();
    }

    private static string BelowThousand(int number)
    {
        var builder = new StringBuilder();

        if (number >= 100)
        {
            Append(builder, _ones[number / 100] + " hundred");
            number %= 100;
        }

        if (number >= 20)
        {
            var tens = _tens[number / 10];

            Append(builder, number % 10 == 0 ? tens : tens + "-" + _ones[number % 10]);
        }
        else if (number > 0)
        {
            Append(builder, _ones[number]);
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string words)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(words);
    }
}
