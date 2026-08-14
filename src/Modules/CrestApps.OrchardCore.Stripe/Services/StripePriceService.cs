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
            UnitAmount = StripeCurrency.ToMinorUnits(model.Amount ?? 0, model.Currency),
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
        // Auto-paging enumerates every price. A plain ListAsync() only returns the first page (Stripe's
        // default limit of 10), which previously caused the sync to treat existing prices as missing and
        // recreate/deactivate them incorrectly once more than a page of prices existed.
        var results = new List<PriceResponse>();

        var options = new PriceListOptions
        {
            Limit = 100,
        };

        await foreach (var price in _priceService.ListAutoPagingAsync(options))
        {
            results.Add(new PriceResponse()
            {
                Id = price.Id,
                LookupKey = price.LookupKey,
                Title = price.Nickname,
                ProductId = price.ProductId,
                IsActive = price.Active,
            });
        }

        return results.ToArray();
    }
}
