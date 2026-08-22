using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Provides Stripe price operations for subscription billing prices.
/// </summary>
public sealed class StripePriceService : IStripePriceService
{
    private readonly PriceService _priceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripePriceService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to create the price service.</param>
    public StripePriceService(StripeClient stripeClient)
    {
        _priceService = new PriceService(stripeClient);
    }

    /// <summary>
    /// Gets a Stripe price by its lookup key.
    /// </summary>
    /// <param name="lookupKey">The Stripe lookup key assigned to the price.</param>
    /// <returns>The matching price details, or <see langword="null"/> when no price exists.</returns>
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

    /// <summary>
    /// Creates a recurring Stripe price for a product.
    /// </summary>
    /// <param name="model">The price creation request.</param>
    /// <returns>The created Stripe price details.</returns>
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

    /// <summary>
    /// Updates mutable metadata for an existing Stripe price identified by lookup key.
    /// </summary>
    /// <param name="lookupKey">The lookup key assigned to the Stripe price.</param>
    /// <param name="model">The price update request.</param>
    /// <returns>The updated Stripe price details.</returns>
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

    /// <summary>
    /// Lists all Stripe prices available to the configured Stripe account.
    /// </summary>
    /// <returns>The complete set of Stripe price details.</returns>
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
