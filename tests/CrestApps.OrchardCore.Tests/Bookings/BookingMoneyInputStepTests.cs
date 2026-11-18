using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Currencies;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// Found with K9 (fifth fix verification): the Bookings money inputs used a fixed <c>step="0.01"</c>, so a browser refused
/// a three-decimal amount such as a KWD refund of 12.345 until staff rounded it. A money input's step now follows the
/// currency's decimal places, or is "any" when the currency isn't known.
/// </summary>
public sealed class BookingMoneyInputStepTests
{
    private static readonly Regex _inputTagRegex = new(@"<input\b[^>]*>", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _stepRegex = new(@"\bstep\s*=\s*""([^""]*)""", RegexOptions.Compiled);

    [Theory]
    [InlineData("USD", "0.01")]
    [InlineData("KWD", "0.001")]
    [InlineData("JPY", "1")]
    [InlineData("CLF", "0.0001")]
    [InlineData(null, "any")]
    [InlineData("", "any")]
    public void GetInputStep_FollowsTheCurrencysDecimalPlaces(string currency, string expected)
        => Assert.Equal(expected, CurrencyScale.GetInputStep(currency));

    // The money inputs of the booking pages: prices, price changes and refunds. Percent and minute fields keep their own step.
    [Theory]
    [InlineData("Views/BookingPart.Edit.cshtml", "Occupancies[i].PriceAdjustment")]
    [InlineData("Views/BookingPart.Edit.cshtml", "WeekendPrice")]
    [InlineData("Views/BookingsAdmin/Dates.cshtml", "NewPrice")]
    [InlineData("Views/BookingsAdmin/Dates.cshtml", "price")]
    [InlineData("Views/BookingsAdmin/Detail.cshtml", "refund")]
    public void MoneyInputs_TakeTheirStepFromTheCurrency(string view, string field)
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Bookings", view.Replace('/', Path.DirectorySeparatorChar));
        var input = _inputTagRegex.Matches(File.ReadAllText(path))
            .Select(match => match.Value)
            .Single(tag => tag.Contains($"asp-for=\"{field}\"", StringComparison.Ordinal) || tag.Contains($"name=\"{field}\"", StringComparison.Ordinal));

        var step = _stepRegex.Match(input).Groups[1].Value;

        Assert.True(step.Contains("CurrencyScale.GetInputStep(", StringComparison.Ordinal) || step.StartsWith('@'), $"The {field} input of {view} has the fixed step \"{step}\", which refuses an amount with more decimals: {input}");
        Assert.DoesNotMatch(@"^\d*\.?\d+$", step);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
