using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripePaymentService
{
    Task<CreatePaymentIntentResponse> CreateAsync(CreatePaymentIntentRequest model);
}

public interface IStripePlanService
{
    Task<PlanResponse> CreateAsync(CreatePlanRequest model);

    Task<PlanResponse> GetAsync(string id);

    Task<PlanResponse> UpdateAsync(string id, UpdatePlanRequest model);
}

public interface IStripeProductService
{
    Task<ProductResponse> CreateAsync(CreateProductRequest model);

    Task<ProductResponse> GetAsync(string id);

    Task<ProductResponse> UpdateAsync(string id, UpdateProductRequest model);
}
