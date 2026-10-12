using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Provides Stripe product operations for products synchronized from Orchard Core.
/// </summary>
public sealed class StripeProductService : IStripeProductService
{
    private readonly ProductService _productService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeProductService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to create the product service.</param>
    public StripeProductService(StripeClient stripeClient)
    {
        _productService = new ProductService(stripeClient);
    }

    /// <summary>
    /// Creates a Stripe product from the supplied product request.
    /// </summary>
    /// <param name="model">The product creation request.</param>
    /// <returns>The created Stripe product details.</returns>
    public async Task<ProductResponse> CreateAsync(CreateProductRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var productOptions = new ProductCreateOptions
        {
            Id = model.Id,
            Name = model.Title,
            Description = model.Description,
            // Valid values 'good' or 'service'. Digital products are billed as services in Stripe.
            Type = model.Type switch
            {
                ProductType.Good => "good",
                _ => "service",
            },
        };

        var product = await _productService.CreateAsync(productOptions);

        return new ProductResponse()
        {
            Id = product.Id,
            Title = product.Name,
            Description = product.Description,
            Type = product.Type,
            IsActive = product.Active,
        };
    }

    /// <summary>
    /// Updates an existing Stripe product.
    /// </summary>
    /// <param name="id">The Stripe product identifier.</param>
    /// <param name="model">The product update request.</param>
    /// <returns>The updated Stripe product details.</returns>
    public async Task<ProductResponse> UpdateAsync(string id, UpdateProductRequest model)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(model);

        var productOptions = new ProductUpdateOptions
        {
            Name = model.Title,
            Description = model.Description,
            Active = model.IsActive,
        };

        var product = await _productService.UpdateAsync(id, productOptions);

        return new ProductResponse()
        {
            Id = product.Id,
            Title = product.Name,
            Description = product.Description,
            Type = product.Type,
            IsActive = product.Active,
        };
    }

    /// <summary>
    /// Gets a Stripe product by identifier.
    /// </summary>
    /// <param name="id">The Stripe product identifier.</param>
    /// <returns>The matching product details, or <see langword="null"/> when no product exists.</returns>
    public async Task<ProductResponse> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        Product product;

        try
        {
            product = await _productService.GetAsync(id);
        }
        catch (StripeException ex)
        {
            // Check if the error indicates that the resource does not exist.
            if (ex.StripeError.Type == "invalid_request_error" && ex.StripeError.Code == "resource_missing")
            {
                return null;
            }

            throw;
        }

        return new ProductResponse()
        {
            Id = product.Id,
            Title = product.Name,
            Description = product.Description,
            Type = product.Type,
            IsActive = product.Active,
        };
    }
}
