namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Provides the managed currencies that products and subscriptions may use.
/// </summary>
public interface IProductCurrencyProvider
{
    /// <summary>
    /// Gets the managed currencies that editors may select.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The managed currencies.</returns>
    ValueTask<IReadOnlyList<CurrencyDefinition>> GetCurrenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a managed currency by its ISO-4217 code.
    /// </summary>
    /// <param name="currencyCode">The ISO-4217 currency code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching managed currency, or <see langword="null"/> when it is not found.</returns>
    ValueTask<CurrencyDefinition> FindByCodeAsync(string currencyCode, CancellationToken cancellationToken = default);
}
