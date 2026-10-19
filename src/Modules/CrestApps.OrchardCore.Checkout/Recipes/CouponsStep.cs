using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Checkout.Recipes;

/// <summary>
/// The "Coupons" step: creates or updates coupons, found by code. How often a coupon was used belongs to the site it
/// was used on, so an import never changes it.
/// </summary>
internal sealed class CouponsStep : NamedRecipeStepHandler
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly ICouponStore _couponStore;

    public CouponsStep(ICouponStore couponStore)
        : base(CheckoutConstants.CouponsRecipeStep)
    {
        _couponStore = couponStore;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in (context.Step["Coupons"] as JsonArray ?? []).OfType<JsonObject>())
        {
            Coupon source;

            try
            {
                source = token.Deserialize<Coupon>(_options);
            }
            catch (JsonException exception)
            {
                context.Errors.Add($"A coupon in the '{CheckoutConstants.CouponsRecipeStep}' step could not be read: {exception.Message}");

                continue;
            }

            var code = source?.Code?.Trim();

            if (string.IsNullOrEmpty(code))
            {
                context.Errors.Add($"A coupon in the '{CheckoutConstants.CouponsRecipeStep}' step has no code.");

                continue;
            }

            var error = Validate(source);

            if (error is not null)
            {
                context.Errors.Add($"Coupon '{code}': {error}");

                continue;
            }

            var coupon = await _couponStore.GetByCodeAsync(code);

            if (coupon is null)
            {
                coupon = new Coupon { ItemId = IdGenerator.GenerateId() };
                Copy(source, coupon);

                await _couponStore.CreateAsync(coupon);
            }
            else
            {
                Copy(source, coupon);

                await _couponStore.UpdateAsync(coupon);
            }
        }
    }

    internal static string Validate(Coupon coupon)
    {
        if (coupon.Kind is CouponKind.Percentage or CouponKind.BuyXGetY && coupon.Percentage is <= 0m or > 100m)
        {
            return "a percentage or buy X get Y coupon needs a Percentage between 1 and 100.";
        }

        if (coupon.Kind == CouponKind.BuyXGetY && (coupon.BuyQuantity < 1 || coupon.GetQuantity < 1))
        {
            return "a buy X get Y coupon needs a BuyQuantity and a GetQuantity of 1 or more.";
        }

        if (coupon.Kind == CouponKind.FixedAmount && (coupon.Amount <= 0m || string.IsNullOrWhiteSpace(coupon.Currency)))
        {
            return "a fixed-amount coupon needs an Amount greater than zero and a Currency.";
        }

        if (coupon.StartsUtc.HasValue && coupon.EndsUtc.HasValue && coupon.EndsUtc.Value <= coupon.StartsUtc.Value)
        {
            return "EndsUtc must be after StartsUtc.";
        }

        if (coupon.MaxRedemptions is <= 0 || coupon.MaxRedemptionsPerCustomer is <= 0)
        {
            return "usage limits must be greater than zero, or left out.";
        }

        return null;
    }

    private static void Copy(Coupon source, Coupon target)
    {
        target.Code = source.Code.Trim();
        target.Method = source.Method;
        target.OnlyGeneratedCodes = source.OnlyGeneratedCodes;
        target.Description = source.Description;
        target.Kind = source.Kind;
        target.Percentage = source.Kind is CouponKind.Percentage or CouponKind.BuyXGetY ? source.Percentage : 0m;
        target.Amount = source.Kind == CouponKind.FixedAmount ? source.Amount : 0m;
        target.Currency = source.Kind == CouponKind.FixedAmount ? source.Currency?.Trim().ToUpperInvariant() : null;
        target.Target = source.Kind is CouponKind.FreeShipping or CouponKind.BuyXGetY ? DiscountTarget.OneTime : source.Target;
        target.BuyQuantity = source.BuyQuantity;
        target.GetQuantity = source.GetQuantity;
        target.MaxApplicationsPerOrder = source.MaxApplicationsPerOrder;
        target.BuyProductIds = [.. source.BuyProductIds ?? []];
        target.BuyCategoryIds = [.. source.BuyCategoryIds ?? []];
        target.MinimumQuantity = source.MinimumQuantity;
        target.CombinesWithProductDiscounts = source.CombinesWithProductDiscounts;
        target.CombinesWithOrderDiscounts = source.CombinesWithOrderDiscounts;
        target.CombinesWithShippingDiscounts = source.CombinesWithShippingDiscounts;
        target.IsEnabled = source.IsEnabled;
        target.StartsUtc = source.StartsUtc;
        target.EndsUtc = source.EndsUtc;
        target.MaxRedemptions = source.MaxRedemptions;
        target.MaxRedemptionsPerCustomer = source.MaxRedemptionsPerCustomer;
        target.MinimumAmount = source.MinimumAmount;
        target.MaximumAmount = source.MaximumAmount;
        target.ProductIds = [.. source.ProductIds ?? []];
        target.CategoryIds = [.. source.CategoryIds ?? []];
        target.Sources = [.. source.Sources ?? []];
        target.ReferenceTypes = [.. source.ReferenceTypes ?? []];
        target.CustomerId = source.CustomerId;
        target.CustomerEmail = source.CustomerEmail;
    }
}
