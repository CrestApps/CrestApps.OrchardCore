using System.Globalization;

namespace CrestApps.OrchardCore;

/// <summary>
/// Converts floating-point values, as returned by search engines and databases, to <see cref="decimal"/>.
/// </summary>
/// <remarks>
/// Since .NET 11 an explicit <c>(decimal)</c> cast or <see cref="Convert.ToDecimal(double)"/> of a <see cref="double"/>
/// keeps every binary digit, so <c>4.3d</c> becomes <c>4.2999999999999998223643160600m</c>. A price or a range
/// bound has to read as the number that was written, so the value is converted through its shortest round-trip
/// representation instead.
/// </remarks>
public static class DecimalConversion
{
    /// <summary>
    /// Converts a <see cref="double"/> to the <see cref="decimal"/> that has its shortest round-trip representation.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="OverflowException">The value is infinite, not a number, or outside the range of <see cref="decimal"/>.</exception>
    public static decimal FromDouble(double value)
    {
        return decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts a value to <see cref="decimal"/>, reading a <see cref="double"/> or <see cref="float"/> through
    /// its shortest round-trip representation.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    public static decimal FromObject(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            double d => FromDouble(d),
            float f => FromDouble(f),
            _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        };
    }
}
