using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripePriceService : IStripePriceService
{
    private readonly PriceService _priceService;

    public StripePriceService(StripeClient stripeClient)
    {
        _priceService = new PriceService(stripeClient);
    }

    public async Task<PriceResponse> GetAsync(string lookupKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(lookupKey);

        var prices = await _priceService.ListAsync(new PriceListOptions()
        {
            LookupKeys = [lookupKey],
            Limit = 1,
        });

        var price = prices.Data.FirstOrDefault();

        if (price == null)
        {
            return null;
        }

        return new PriceResponse()
        {
            Id = price.Id,
            LookupKey = price.LookupKey,
            Title = price.Nickname,
            ProductId = price.ProductId,
            IsActive = price.Active,
        };
    }

    public async Task<PriceResponse> CreateAsync(CreatePriceRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var planOptions = new PriceCreateOptions
        {
            LookupKey = model.LookupKey,
            Product = model.ProductId,
            Nickname = model.Title,
            UnitAmount = (long)((model.Amount ?? 0) * 100),
            Currency = model.Currency,
            Recurring = new PriceRecurringOptions()
            {
                Interval = model.Interval,
                IntervalCount = model.IntervalCount,
            },
        };

        var price = await _priceService.CreateAsync(planOptions);

        return new PriceResponse()
        {
            Id = price.Id,
            LookupKey = price.LookupKey,
            Title = price.Nickname,
            ProductId = price.ProductId,
            IsActive = price.Active,
        };
    }

    public async Task<PriceResponse> UpdateAsync(string lookupKey, UpdatePriceRequest model)
    {
        ArgumentException.ThrowIfNullOrEmpty(lookupKey);
        ArgumentNullException.ThrowIfNull(model);

        var price = await GetAsync(lookupKey)
            ?? throw new ArgumentOutOfRangeException(nameof(lookupKey), "Unable to find the given price ID.");

        var planOptions = new PriceUpdateOptions
        {
            Nickname = model.Title,
            Active = model.IsActive,
        };

        var result = await _priceService.UpdateAsync(price.Id, planOptions);

        return new PriceResponse()
        {
            Id = result.Id,
            LookupKey = lookupKey,
            Title = result.Nickname,
            ProductId = result.ProductId,
        };
    }

    public async Task<PriceResponse[]> ListAsync()
    {
        var prices = await _priceService.ListAsync();

        return prices.Select(price => new PriceResponse()
        {
            Id = price.Id,
            LookupKey = price.LookupKey,
            Title = price.Nickname,
            ProductId = price.ProductId,
            IsActive = price.Active,
        }).ToArray();
    }
}
