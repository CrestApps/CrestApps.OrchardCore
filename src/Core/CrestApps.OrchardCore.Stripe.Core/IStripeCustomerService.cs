using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripeCustomerService
{
    Task<CreateCustomerResponse> CreateAsync(CreateCustomerRequest model);
    Task<CustomerResponse> GetAsync(string id);
    Task<UpdateCustomerResponse> UpdateAsync(string id, UpdateCustomerRequest model);
}
