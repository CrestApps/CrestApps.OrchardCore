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
    /// Fills the coupon's form from a stored coupon, as the editor opens it. Its dates are left in UTC
    /// (<see cref="CouponEditViewModel.StartsUtc"/> and <see cref="CouponEditViewModel.EndsUtc"/>); the editor shows them
    /// in the site's time zone.
    /// </summary>
    /// <param name="coupon">The stored coupon.</param>
    /// <returns>The form's values.</returns>
    public static CouponEditViewModel ToModel(Coupon coupon)
    {
        ArgumentNullException.ThrowIfNull(coupon);

        return new CouponEditViewModel
        {
            ItemId = coupon.ItemId,
            Code = coupon.Code,
            Method = coupon.Method,
            OnlyGeneratedCodes = coupon.OnlyGeneratedCodes,
            Description = coupon.Description,
            Kind = coupon.Kind,
            Percentage = coupon.Percentage,
            Amount = coupon.Amount,
            Currency = coupon.Currency,
            Target = coupon.Target,
            IsEnabled = coupon.IsEnabled,
            StartsUtc = coupon.StartsUtc,
            EndsUtc = coupon.EndsUtc,
            MaxRedemptions = coupon.MaxRedemptions,
            MinimumAmount = coupon.MinimumAmount,
            MaximumAmount = coupon.MaximumAmount,
            MaxRedemptionsPerCustomer = coupon.MaxRedemptionsPerCustomer,
            MinimumQuantity = coupon.MinimumQuantity,
            BuyQuantity = coupon.BuyQuantity > 0 ? coupon.BuyQuantity : null,
            GetQuantity = coupon.GetQuantity > 0 ? coupon.GetQuantity : null,
            MaxApplicationsPerOrder = coupon.MaxApplicationsPerOrder,
            CombinesWithProductDiscounts = coupon.CombinesWithProductDiscounts,
            CombinesWithOrderDiscounts = coupon.CombinesWithOrderDiscounts,
            CombinesWithShippingDiscounts = coupon.CombinesWithShippingDiscounts,
            RedemptionCount = coupon.RedemptionCount,
            Sources = string.Join(", ", coupon.Sources ?? []),
            ReferenceTypes = string.Join(", ", coupon.ReferenceTypes ?? []),
            CustomerId = coupon.CustomerId,
            CustomerEmail = coupon.CustomerEmail,
        };
    }

    /// <summary>
    /// Checks a request to generate codes for a coupon: not for an automatic discount, 1 to
    /// <see cref="CheckoutConstants.MaxGeneratedCodes"/> codes at a time, a prefix of up to 12 letters, digits or dashes,
    /// and a limit per code above zero when one is given.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="count">How many codes to generate.</param>
    /// <param name="prefix">What every code starts with, already trimmed and in upper case, or <see langword="null"/>.</param>
    /// <param name="usesPerCode">How many times each code may be used, or <see langword="null"/> for no limit.</param>
    /// <returns>What is wrong, or <see cref="CouponCodeGenerationProblem.None"/>.</returns>
    public static CouponCodeGenerationProblem CheckCodeGeneration(Coupon coupon, int count, string prefix, int? usesPerCode)
    {
        ArgumentNullException.ThrowIfNull(coupon);

        if (coupon.IsAutomatic)
        {
            return CouponCodeGenerationProblem.Automatic;
        }

        if (count is < 1 or > CheckoutConstants.MaxGeneratedCodes)
        {
            return CouponCodeGenerationProblem.Count;
        }

        if (!string.IsNullOrEmpty(prefix) && (prefix.Length > 12 || !prefix.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')))
        {
            return CouponCodeGenerationProblem.Prefix;
        }

        return usesPerCode is <= 0
            ? CouponCodeGenerationProblem.UsesPerCode
            : CouponCodeGenerationProblem.None;
    }

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

/// <summary>
/// What is wrong with a request to generate codes for a coupon.
/// </summary>
public enum CouponCodeGenerationProblem
{
    /// <summary>
    /// Nothing; the codes can be generated.
    /// </summary>
    None,

    /// <summary>
    /// The coupon is an automatic discount, which has no codes.
    /// </summary>
    Automatic,

    /// <summary>
    /// The count is not between 1 and the most generated at a time.
    /// </summary>
    Count,

    /// <summary>
    /// The prefix is longer than 12 characters or has characters other than letters, digits and dashes.
    /// </summary>
    Prefix,

    /// <summary>
    /// The limit per code is zero or less.
    /// </summary>
    UsesPerCode,
}
