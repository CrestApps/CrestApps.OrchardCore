using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ExchangeRates.Models;

/// <summary>
/// What one unit of a currency was worth in another on a day: <c>1 {Currency} = {Rate} {BaseCurrency}</c>. A rate holds
/// from its day until a later one of the same pair.
/// </summary>
public sealed class ExchangeRate : CatalogItem
{
    /// <summary>
    /// Gets or sets the ISO 4217 code of the currency the rate converts, such as EUR.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the ISO 4217 code of the currency the rate converts to, usually the one the store reports in.
    /// </summary>
    public string BaseCurrency { get; set; }

    /// <summary>
    /// Gets or sets how many units of <see cref="BaseCurrency"/> one unit of <see cref="Currency"/> is worth.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets the day the rate takes effect.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets where the rate came from: <c>manual</c>, or the name of the provider that fetched it.
    /// </summary>
    public string Source { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// The rate found for a day.
/// </summary>
/// <param name="Rate">How many units of the base currency one unit of the currency is worth.</param>
/// <param name="Date">The day the rate took effect, on or before the day asked for.</param>
/// <param name="Source">Where the rate came from.</param>
public sealed record ExchangeRateQuote(decimal Rate, DateOnly Date, string Source);

/// <summary>
/// The exchange rate settings: the currency the store reports in, the currencies kept rates for, and the provider that
/// fetches them.
/// </summary>
public sealed class ExchangeRateSettings
{
    /// <summary>
    /// Gets or sets the ISO 4217 code of the currency the store reports in, such as USD.
    /// </summary>
    public string BaseCurrency { get; set; }

    /// <summary>
    /// Gets or sets the currencies a provider fetches rates for each day.
    /// </summary>
    public IList<string> Currencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of the provider that fetches the rates each day, or <see langword="null"/> to enter them by
    /// hand only.
    /// </summary>
    public string ProviderName { get; set; }
}
