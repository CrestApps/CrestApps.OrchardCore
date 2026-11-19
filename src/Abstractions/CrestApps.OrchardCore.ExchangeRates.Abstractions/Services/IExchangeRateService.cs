using CrestApps.Core.Services;
using CrestApps.OrchardCore.ExchangeRates.Models;

namespace CrestApps.OrchardCore.ExchangeRates.Services;

/// <summary>
/// Manages the dated exchange rates.
/// </summary>
public interface IExchangeRateManager : ICatalogManager<ExchangeRate>
{
}

/// <summary>
/// Finds the exchange rate of a day: the latest rate of the pair on or before it, or the inverse of the latest rate of
/// the opposite pair.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>
    /// Gets the currency the store reports in, or <see langword="null"/> when none is set.
    /// </summary>
    Task<string> GetBaseCurrencyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets what one unit of <paramref name="currency"/> was worth in <paramref name="baseCurrency"/> on a day: 1 for
    /// the same currency, else the latest rate of the pair on or before the day, else the inverse of the opposite pair's.
    /// Returns <see langword="null"/> when no rate is known.
    /// </summary>
    Task<ExchangeRateQuote> GetRateAsync(string currency, string baseCurrency, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches exchange rates from somewhere, such as a central bank's daily feed. The provider named in the settings is
/// asked once a day for the rates of the settings' currencies.
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Gets the provider's technical name, stored in the settings and on the rates it fetches.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown to staff.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Fetches what one unit of each currency is worth in <paramref name="baseCurrency"/> on a day. A currency the
    /// provider has no rate for is left out.
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(string baseCurrency, IReadOnlyCollection<string> currencies, DateOnly date, CancellationToken cancellationToken = default);
}
