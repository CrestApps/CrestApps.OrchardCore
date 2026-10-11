using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Provides access to the Stripe Price API for creating, retrieving, listing, and updating prices.
/// </summary>
public interface IStripePriceService
{
    /// <summary>
    /// Creates a new Stripe price.
    /// </summary>
    /// <param name="model">The details of the price to create.</param>
    /// <returns>The created price.</returns>
    Task<PriceResponse> CreateAsync(CreatePriceRequest model);

    /// <summary>
    /// Retrieves a Stripe price by its lookup key.
    /// </summary>
    /// <param name="lookupKey">The price lookup key.</param>
    /// <returns>The matching price.</returns>
    Task<PriceResponse> GetAsync(string lookupKey);

    /// <summary>
    /// Lists the available Stripe prices.
    /// </summary>
    /// <returns>The available prices.</returns>
    Task<PriceResponse[]> ListAsync();

    /// <summary>
    /// Updates a Stripe price identified by its lookup key.
    /// </summary>
    /// <param name="lookupKey">The price lookup key.</param>
    /// <param name="model">The price values to update.</param>
    /// <returns>The updated price.</returns>
    Task<PriceResponse> UpdateAsync(string lookupKey, UpdatePriceRequest model);
}
