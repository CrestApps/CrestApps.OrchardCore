using System.Globalization;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Admin;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Markets.Services;

/// <summary>
/// The market the current shopper buys in, and prices in its currency. A storefront shopper's market is the one they
/// picked (kept in a cookie), or else the one of their language's country; an app names it with the <c>market</c>
/// query value or the <c>X-Market</c> header, by its id or a country code. The admin, and an app that names no market,
/// sell in the products' own currencies.
/// </summary>
public sealed class ShopperMarketService : IShopperCurrency
{
    private readonly IMarketStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;

    private IReadOnlyList<Market> _markets;
    private Market _current;
    private bool _resolved;
    private string _storeCurrency;

    public ShopperMarketService(
        IMarketStore store,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService)
    {
        _store = store;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
    }

    /// <summary>
    /// Returns the markets that are on, by name.
    /// </summary>
    public async Task<IReadOnlyList<Market>> GetMarketsAsync(CancellationToken cancellationToken = default)
        => _markets ??= [.. (await _store.GetAllAsync(cancellationToken))
            .Where(market => market.IsEnabled && !string.IsNullOrEmpty(market.Currency) && market.ExchangeRate > 0m)
            .OrderBy(market => market.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Returns the market the current shopper buys in, or <see langword="null"/> when they buy in the products' own
    /// currencies.
    /// </summary>
    public async Task<Market> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved)
        {
            return _current;
        }

        _resolved = true;

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || AdminAttribute.IsApplied(httpContext))
        {
            return null;
        }

        var markets = await GetMarketsAsync(cancellationToken);

        if (markets.Count == 0)
        {
            return null;
        }

        var request = httpContext.Request;

        _current = Find(markets, request.Query[MarketsConstants.QueryName]) ??
            Find(markets, request.Headers[MarketsConstants.HeaderName]);

        // An app, such as a till, says which market it sells in; it isn't guessed from a browser's cookie or language.
        if (_current is null && !IsApi(request))
        {
            _current = Find(markets, request.Cookies[MarketsConstants.CookieName]) ?? FindByCulture(markets, CultureInfo.CurrentCulture);
        }

        return _current;
    }

    /// <summary>
    /// Returns the market with an id, or the one a country is in.
    /// </summary>
    public static Market Find(IReadOnlyList<Market> markets, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || markets is null)
        {
            return null;
        }

        value = value.Trim();

        return markets.FirstOrDefault(market => string.Equals(market.ItemId, value, StringComparison.Ordinal)) ??
            (value.Length == 2 ? markets.FirstOrDefault(market => market.Covers(value)) : null);
    }

    /// <summary>
    /// Returns the market of a culture's country, such as Germany's for de-DE.
    /// </summary>
    public static Market FindByCulture(IReadOnlyList<Market> markets, CultureInfo culture)
    {
        if (culture is null || string.IsNullOrEmpty(culture.Name) || culture.IsNeutralCulture)
        {
            return null;
        }

        try
        {
            return Find(markets, new RegionInfo(culture.Name).TwoLetterISORegionName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async ValueTask<string> GetCurrencyAsync(CancellationToken cancellationToken = default)
        => (await GetCurrentAsync(cancellationToken))?.Currency;

    public async ValueTask<string> GetStoreCurrencyAsync(CancellationToken cancellationToken = default)
    {
        _storeCurrency ??= (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency ?? string.Empty;

        return _storeCurrency.Length == 0 ? null : _storeCurrency;
    }

    public async ValueTask<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, bool isPrice = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(toCurrency))
        {
            return null;
        }

        // An amount without a currency, such as a shipping rate, is in the store's.
        if (string.IsNullOrEmpty(fromCurrency))
        {
            _storeCurrency ??= (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency ?? string.Empty;
            fromCurrency = _storeCurrency;

            if (string.IsNullOrEmpty(fromCurrency))
            {
                return null;
            }
        }

        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        var current = await GetCurrentAsync(cancellationToken);
        var markets = await GetMarketsAsync(cancellationToken);

        if (await GetRateAsync(fromCurrency, current, markets) is not { } fromRate ||
            await GetRateAsync(toCurrency, current, markets) is not { } toRate)
        {
            return null;
        }

        var converted = MarketPricing.Convert(amount, fromRate, toRate);
        var target = InCurrency(toCurrency, current, markets);

        return isPrice && target is not null
            ? MarketPricing.Round(converted, toCurrency, target.Rounding)
            : CurrencyScale.Round(converted, toCurrency);
    }

    // How much of a currency one unit of the store's currency is worth: one for the store's own, a market's rate for
    // the market's.
    private async Task<decimal?> GetRateAsync(string currency, Market current, IReadOnlyList<Market> markets)
    {
        _storeCurrency ??= (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency ?? string.Empty;

        if (string.Equals(currency, _storeCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        return InCurrency(currency, current, markets)?.ExchangeRate;
    }

    // The market whose rate and rounding a currency's prices follow: the shopper's, when it's in it.
    private static Market InCurrency(string currency, Market current, IReadOnlyList<Market> markets)
        => current is not null && string.Equals(current.Currency, currency, StringComparison.OrdinalIgnoreCase)
            ? current
            : markets.FirstOrDefault(market => string.Equals(market.Currency, currency, StringComparison.OrdinalIgnoreCase));

    private static bool IsApi(HttpRequest request)
        => request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
