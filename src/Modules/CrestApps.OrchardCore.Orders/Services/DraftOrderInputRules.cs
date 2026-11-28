namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The limits of what the store enters for a draft order, shared by the draft order editor and the order AI tools: at
/// most <see cref="MaxLines"/> lines, quantities from zero to <see cref="MaxQuantity"/>, a quoted price from zero to below
/// a hundred million, and a pay-by date kept at noon UTC so it reads the same day in every time zone.
/// </summary>
internal static class DraftOrderInputRules
{
    /// <summary>
    /// The most lines a draft order has.
    /// </summary>
    public const int MaxLines = 100;

    /// <summary>
    /// The most units of one line.
    /// </summary>
    public const int MaxQuantity = 10_000;

    /// <summary>
    /// Returns whether a quoted unit price is accepted.
    /// </summary>
    /// <param name="price">The price.</param>
    /// <returns><see langword="true"/> when it is from zero to below a hundred million.</returns>
    public static bool IsAcceptedPrice(decimal price)
        => price is >= 0 and < 100_000_000;

    /// <summary>
    /// Gets the pay-by date as it is kept: noon UTC of the date.
    /// </summary>
    /// <param name="date">The date to pay by.</param>
    /// <returns>The date at noon UTC.</returns>
    public static DateTime ToDueUtc(DateTime date)
        => DateTime.SpecifyKind(date.Date.AddHours(12), DateTimeKind.Utc);
}
