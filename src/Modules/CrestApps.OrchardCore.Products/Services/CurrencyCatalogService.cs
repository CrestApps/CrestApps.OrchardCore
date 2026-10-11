using CrestApps.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Models;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Exposes the managed currencies stored for products and subscriptions.
/// </summary>
internal sealed class CurrencyCatalogService : IProductCurrencyProvider
{
    private readonly INamedCatalog<CurrencyEntry> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyCatalogService"/> class.
    /// </summary>
    /// <param name="catalog">The currency catalog.</param>
    public CurrencyCatalogService(INamedCatalog<CurrencyEntry> catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc />
    public async ValueTask<CurrencyDefinition> FindByCodeAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = CurrencyCodeUtility.Normalize(currencyCode);

        if (string.IsNullOrEmpty(normalizedCode))
        {
            return null;
        }

        var currency = await _catalog.FindByNameAsync(normalizedCode, cancellationToken);

        return currency == null ? null : ToDefinition(currency);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<CurrencyDefinition>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        var currencies = await _catalog.GetAllAsync(cancellationToken);

        return currencies
            .Where(currency => CurrencyCodeUtility.IsValid(currency.Name) && !string.IsNullOrWhiteSpace(currency.DisplayName))
            .OrderBy(currency => currency.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(currency => currency.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToDefinition)
            .ToArray();
    }

    private static CurrencyDefinition ToDefinition(CurrencyEntry currency)
    {
        return new CurrencyDefinition
        {
            CurrencyCode = currency.Name,
            DisplayName = currency.DisplayName,
        };
    }
}
