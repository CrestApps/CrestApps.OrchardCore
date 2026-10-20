using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using OrchardCore;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The rules of a coupon as a merchant enters it, shared by the coupon admin and the commerce API: what's required,
/// what's in range, a code that's not taken, and how the entered values become the coupon.
/// </summary>
public sealed class CouponEditor
{
    private readonly ICouponStore _couponStore;

    internal readonly IStringLocalizer S;

    public CouponEditor(ICouponStore couponStore, IStringLocalizer<CouponEditor> stringLocalizer)
    {
        _couponStore = couponStore;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks a coupon as entered, adding what's wrong to <paramref name="modelState"/>. An automatic discount left
    /// without a code is given one.
    /// </summary>
    public async Task ValidateAsync(CouponEditViewModel model, string existingItemId, ModelStateDictionary modelState)
    {
        // Nobody types an automatic discount's code, so one is made up when it's left blank; it only names the
        // discount's uses.
        if (model.Method == CouponMethod.Automatic && string.IsNullOrWhiteSpace(model.Code))
        {
            model.Code = "AUTO-" + IdGenerator.GenerateId()[..8].ToUpperInvariant();
        }

        if (model.Method == CouponMethod.Automatic && string.IsNullOrWhiteSpace(model.Description))
        {
            modelState.AddModelError(nameof(model.Description), S["An automatic discount needs a description. It's what the buyer sees."]);
        }

        var code = model.Code?.Trim();

        if (string.IsNullOrEmpty(code))
        {
            modelState.AddModelError(nameof(model.Code), S["A code is required."]);
        }
        else
        {
            // Two coupons sharing a code would make which one applies a matter of query order, so the same
            // customer could get different discounts on different days.
            var existing = await _couponStore.GetByCodeAsync(code);

            if (existing is not null && !string.Equals(existing.ItemId, existingItemId, StringComparison.Ordinal))
            {
                modelState.AddModelError(nameof(model.Code), S["That code is already in use."]);
            }
        }

        if (model.Kind == CouponKind.BuyXGetY)
        {
            if (model.BuyQuantity is not >= 1)
            {
                modelState.AddModelError(nameof(model.BuyQuantity), S["Enter how many items the customer buys, 1 or more."]);
            }

            if (model.GetQuantity is not >= 1)
            {
                modelState.AddModelError(nameof(model.GetQuantity), S["Enter how many items the customer gets, 1 or more."]);
            }

            if (model.MaxApplicationsPerOrder is <= 0)
            {
                modelState.AddModelError(nameof(model.MaxApplicationsPerOrder), S["Leave it blank for no limit, or enter a number greater than zero."]);
            }
        }

        if (model.MinimumQuantity is <= 0)
        {
            modelState.AddModelError(nameof(model.MinimumQuantity), S["Leave the minimum blank, or enter a number greater than zero."]);
        }

        if (model.Kind is CouponKind.Percentage or CouponKind.BuyXGetY && model.Percentage is <= 0m or > 100m)
        {
            modelState.AddModelError(nameof(model.Percentage), S["Enter a percentage between 1 and 100."]);
        }

        if (model.Kind == CouponKind.FixedAmount)
        {
            if (model.Amount <= 0m)
            {
                modelState.AddModelError(nameof(model.Amount), S["Enter an amount greater than zero."]);
            }

            if (string.IsNullOrWhiteSpace(model.Currency))
            {
                // A fixed amount with no currency would apply its face value to any currency, so ten dollars
                // off would become ten thousand yen off.
                modelState.AddModelError(nameof(model.Currency), S["A fixed amount needs a currency."]);
            }
        }

        if (model.StartsUtc.HasValue && model.EndsUtc.HasValue && model.EndsUtc.Value <= model.StartsUtc.Value)
        {
            modelState.AddModelError(nameof(model.EndsUtc), S["The end date must be after the start date."]);
        }

        if (!string.IsNullOrWhiteSpace(model.CustomerEmail) && !model.CustomerEmail.Contains('@', StringComparison.Ordinal))
        {
            modelState.AddModelError(nameof(model.CustomerEmail), S["Enter the customer's email address, or leave it blank."]);
        }

        if (model.MaxRedemptions is <= 0)
        {
            modelState.AddModelError(nameof(model.MaxRedemptions), S["Leave the limit blank for unlimited, or enter a number greater than zero."]);
        }

        if (model.MaxRedemptionsPerCustomer is <= 0)
        {
            modelState.AddModelError(nameof(model.MaxRedemptionsPerCustomer), S["Leave the limit blank for no limit, or enter a number greater than zero."]);
        }

        if (model.MaximumAmount is <= 0m)
        {
            modelState.AddModelError(nameof(model.MaximumAmount), S["Leave the maximum blank, or enter an amount greater than zero."]);
        }
        else if (model.MaximumAmount.HasValue && model.MinimumAmount.HasValue && model.MaximumAmount.Value < model.MinimumAmount.Value)
        {
            modelState.AddModelError(nameof(model.MaximumAmount), S["The maximum amount can't be less than the minimum amount."]);
        }
    }

    private static List<string> SplitList(string value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string TrimToNull(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    /// <summary>
    /// Applies a valid coupon as entered.
    /// </summary>
    public static void Apply(CouponEditViewModel model, Coupon coupon)
    {
        coupon.Code = model.Code?.Trim();
        coupon.Method = model.Method;
        coupon.OnlyGeneratedCodes = model.Method == CouponMethod.Code && model.OnlyGeneratedCodes;
        coupon.Description = model.Description?.Trim();
        coupon.Kind = model.Kind;
        coupon.Percentage = model.Kind is CouponKind.Percentage or CouponKind.BuyXGetY ? model.Percentage : 0m;
        coupon.Amount = model.Kind == CouponKind.FixedAmount ? model.Amount : 0m;
        coupon.Currency = model.Kind == CouponKind.FixedAmount ? model.Currency?.Trim().ToUpperInvariant() : null;
        // Free shipping takes off shipping, whatever it was set to apply to before, and a buy X get Y offer takes
        // something off items bought now.
        coupon.Target = model.Kind is CouponKind.FreeShipping or CouponKind.BuyXGetY ? DiscountTarget.OneTime : model.Target;
        coupon.BuyQuantity = model.Kind == CouponKind.BuyXGetY ? model.BuyQuantity ?? 0 : 0;
        coupon.GetQuantity = model.Kind == CouponKind.BuyXGetY ? model.GetQuantity ?? 0 : 0;
        coupon.MaxApplicationsPerOrder = model.Kind == CouponKind.BuyXGetY ? model.MaxApplicationsPerOrder : null;
        coupon.MinimumQuantity = model.MinimumQuantity;

        if (model.Kind != CouponKind.BuyXGetY)
        {
            coupon.BuyProductIds = [];
            coupon.BuyCategoryIds = [];
        }
        coupon.IsEnabled = model.IsEnabled;
        coupon.StartsUtc = model.StartsUtc;
        coupon.EndsUtc = model.EndsUtc;
        coupon.MaxRedemptions = model.MaxRedemptions;
        coupon.MinimumAmount = model.MinimumAmount;
        coupon.MaximumAmount = model.MaximumAmount;
        coupon.MaxRedemptionsPerCustomer = model.MaxRedemptionsPerCustomer;
        coupon.CombinesWithProductDiscounts = model.CombinesWithProductDiscounts;
        coupon.CombinesWithOrderDiscounts = model.CombinesWithOrderDiscounts;
        coupon.CombinesWithShippingDiscounts = model.CombinesWithShippingDiscounts;
        coupon.Sources = SplitList(model.Sources);
        coupon.ReferenceTypes = SplitList(model.ReferenceTypes);
        coupon.CustomerId = TrimToNull(model.CustomerId);
        coupon.CustomerEmail = TrimToNull(model.CustomerEmail);
    }
}
