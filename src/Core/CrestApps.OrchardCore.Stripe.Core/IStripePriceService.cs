using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripePriceService
{
    Task<PriceResponse> CreateAsync(CreatePriceRequest model);

    Task<PriceResponse> GetAsync(string lookupKey);

    Task<PriceResponse[]> ListAsync();

    Task<PriceResponse> UpdateAsync(string lookupKey, UpdatePriceRequest model);
}
