namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Helpers for working with monetary amounts that are represented as <see cref="double"/> values.
///
/// Payment gateways such as Stripe exchange money using integer minor units (e.g. cents), whereas this
/// codebase carries amounts as <see cref="double"/> dollars. Comparing those amounts with the default
/// <c>==</c>/<c>!=</c> operators is unsafe because binary floating point cannot represent most decimal
/// fractions exactly (for example <c>19.99 + 10.00</c> is not guaranteed to equal <c>29.99</c>). That
/// imprecision can cause a valid payment to be rejected, or two different amounts to be treated as equal.
///
/// All comparisons here are performed after converting the amounts to whole minor units, which is the
/// smallest unit any supported gateway settles in, so the result is deterministic and gateway aligned.
/// </summary>
public static class Money
{
    /// <summary>
    /// Rounds a monetary amount to two decimal places using away-from-zero (arithmetic) rounding.
    /// </summary>
    public static double Round(double amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts a monetary amount expressed in major units (e.g. dollars) to whole minor units (e.g. cents).
    /// </summary>
    public static long ToMinorUnits(double amount)
        => (long)Math.Round(amount * 100d, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Determines whether two monetary amounts are equal once normalized to whole minor units.
    /// </summary>
    public static bool AreEqual(double left, double right)
        => ToMinorUnits(left) == ToMinorUnits(right);

    /// <summary>
    /// Determines whether two monetary amounts are equal once normalized to whole minor units.
    /// A <see langword="null"/> amount is treated as not equal to any value, including another <see langword="null"/>.
    /// </summary>
    public static bool AreEqual(double? left, double? right)
        => left.HasValue && right.HasValue && AreEqual(left.Value, right.Value);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="amount"/> is strictly greater than
    /// <paramref name="threshold"/> after both are normalized to whole minor units.
    /// </summary>
    public static bool IsGreaterThan(double amount, double threshold)
        => ToMinorUnits(amount) > ToMinorUnits(threshold);
}
