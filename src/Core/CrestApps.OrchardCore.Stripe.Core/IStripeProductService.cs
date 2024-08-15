using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripeProductService
{
    Task<ProductResponse> CreateAsync(CreateProductRequest model);

    Task<ProductResponse> GetAsync(string id);

    Task<ProductResponse> UpdateAsync(string id, UpdateProductRequest model);
}
