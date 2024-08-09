using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripePlanService
{
    Task<PlanResponse> CreateAsync(CreatePlanRequest model);

    Task<PlanResponse> GetAsync(string id);

    Task<PlanResponse> UpdateAsync(string id, UpdatePlanRequest model);
}
