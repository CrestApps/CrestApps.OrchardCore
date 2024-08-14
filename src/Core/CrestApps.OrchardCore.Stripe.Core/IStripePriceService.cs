using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripePriceService
{
    Task<PriceResponse> CreateAsync(CreatePriceRequest model);

    Task<PriceResponse> GetAsync(string id);

    Task<PriceResponse> UpdateAsync(string id, UpdatePriceRequest model);
}
