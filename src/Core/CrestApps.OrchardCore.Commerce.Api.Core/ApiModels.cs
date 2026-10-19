using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Commerce.Api;

/// <summary>
/// An amount of money: the amount in major units, rounded at the currency's precision, and its ISO-4217 currency.
/// </summary>
public sealed record ApiMoney(decimal Amount, string Currency)
{
    public static ApiMoney From(decimal amount, string currency)
        => new(Money.Round(amount, currency), currency);

    public static ApiMoney From(decimal? amount, string currency)
        => amount is null ? null : From(amount.Value, currency);
}

/// <summary>
/// A page of results: the items, how many match in all, and where the page starts.
/// </summary>
public sealed record ApiPage<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take)
{
    /// <summary>
    /// The most items one page returns.
    /// </summary>
    public const int MaxTake = 100;

    /// <summary>
    /// Returns a page size between 1 and <see cref="MaxTake"/>, <paramref name="defaultTake"/> when none was asked for.
    /// </summary>
    public static int ClampTake(int? take, int defaultTake = 20)
        => Math.Clamp(take ?? defaultTake, 1, MaxTake);

    public static int ClampSkip(int? skip)
        => Math.Max(0, skip ?? 0);
}
