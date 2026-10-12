using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Provides access to the Stripe Product API for creating, retrieving, and updating products.
/// </summary>
public interface IStripeProductService
{
    /// <summary>
    /// Creates a new Stripe product.
    /// </summary>
    /// <param name="model">The details of the product to create.</param>
    /// <returns>The created product.</returns>
    Task<ProductResponse> CreateAsync(CreateProductRequest model);

    /// <summary>
    /// Retrieves a Stripe product by identifier.
    /// </summary>
    /// <param name="id">The Stripe product identifier.</param>
    /// <returns>The matching product.</returns>
    Task<ProductResponse> GetAsync(string id);

    /// <summary>
    /// Updates an existing Stripe product.
    /// </summary>
    /// <param name="id">The Stripe product identifier.</param>
    /// <param name="model">The product values to update.</param>
    /// <returns>The updated product.</returns>
    Task<ProductResponse> UpdateAsync(string id, UpdateProductRequest model);
}
