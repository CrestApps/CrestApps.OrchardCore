using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripeProductService : IStripeProductService
{
    private readonly ProductService _productService;

    public StripeProductService(StripeClient stripeClient)
    {
        _productService = new ProductService(stripeClient);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var productOptions = new ProductCreateOptions
        {
            Id = model.Id,
            Name = model.Title,
            Description = model.Description,
            // Valid values 'good', 'service', or 'planet'
            Type = model.Type switch
            {
                ProductType.Good => "good",
                ProductType.Planet => "planet",
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
