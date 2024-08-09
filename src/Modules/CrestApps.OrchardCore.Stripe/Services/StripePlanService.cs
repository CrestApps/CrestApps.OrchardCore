using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripePlanService : IStripePlanService
{
    private readonly PlanService _productService;

    public StripePlanService(StripeClient stripeClient)
    {
        _productService = new PlanService(stripeClient);
    }

    public async Task<PlanResponse> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        Plan plan;

        try
        {
            plan = await _productService.GetAsync(id);
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

        return new PlanResponse()
        {
            Id = plan.Id,
            Title = plan.Nickname,
            ProductId = plan.ProductId,
            IsActive = plan.Active,
        };
    }

    public async Task<PlanResponse> CreateAsync(CreatePlanRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var planOptions = new PlanCreateOptions
        {
            Id = model.Id,
            Product = model.ProductId,
            Nickname = model.Title,
            Amount = (int)((model.Amount ?? 0) * 100),
            Currency = model.Currency,
            Interval = model.Interval.ToLowerInvariant(),
            IntervalCount = model.IntervalCount,
        };

        var plan = await _productService.CreateAsync(planOptions);

        return new PlanResponse()
        {
            Id = plan.Id,
            Title = plan.Nickname,
            ProductId = plan.ProductId,
            IsActive = plan.Active,
        };
    }

    public async Task<PlanResponse> UpdateAsync(string id, UpdatePlanRequest model)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(model);

        var planOptions = new PlanUpdateOptions
        {
            Product = model.ProductId,
            Nickname = model.Title,
            Active = model.IsActive,
        };

        var plan = await _productService.UpdateAsync(id, planOptions);

        return new PlanResponse()
        {
            Id = plan.Id,
            Title = plan.Nickname,
            ProductId = plan.ProductId,
        };
    }
}
