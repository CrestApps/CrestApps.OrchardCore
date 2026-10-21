using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Checkout.Recipes;

/// <summary>
/// The "Coupons" step: creates or updates coupons, found by code, with the same rules and mapping as the coupon editor
/// (<see cref="CouponEditor"/>). How often a coupon was used belongs to the site it was used on, so an import never
/// changes it.
/// </summary>
internal sealed class CouponsStep : NamedRecipeStepHandler
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly ICouponStore _couponStore;
    private readonly CouponEditor _couponEditor;

    public CouponsStep(
        ICouponStore couponStore,
        CouponEditor couponEditor)
        : base(CheckoutConstants.CouponsRecipeStep)
    {
        _couponStore = couponStore;
        _couponEditor = couponEditor;
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

            // The step finds a coupon by its code, so a coupon without one, even an automatic discount, is refused
            // rather than made up anew each time the recipe runs.
            var code = source?.Code?.Trim();

            if (string.IsNullOrEmpty(code))
            {
                context.Errors.Add($"A coupon in the '{CheckoutConstants.CouponsRecipeStep}' step has no code.");

                continue;
            }

            var coupon = await _couponStore.GetByCodeAsync(code);

            // The admin editor's rules and mapping, so a recipe can't store a coupon the editor would refuse.
            var model = ToModel(source);
            var modelState = new ModelStateDictionary();

            await _couponEditor.ValidateAsync(model, coupon?.ItemId, modelState);

            if (!modelState.IsValid)
            {
                var errors = modelState.Values.SelectMany(entry => entry.Errors).Select(error => error.ErrorMessage);

                context.Errors.Add($"Coupon '{code}': {string.Join(" ", errors)}");

                continue;
            }

            var isNew = coupon is null;

            coupon ??= new Coupon { ItemId = IdGenerator.GenerateId() };

            CouponEditor.Apply(model, coupon);

            // The products and categories are picked beside the editor's fields, so they are copied as they are.
            coupon.ProductIds = [.. source.ProductIds ?? []];
            coupon.CategoryIds = [.. source.CategoryIds ?? []];

            if (coupon.Kind == CouponKind.BuyXGetY)
            {
                coupon.BuyProductIds = [.. source.BuyProductIds ?? []];
                coupon.BuyCategoryIds = [.. source.BuyCategoryIds ?? []];
            }

            if (isNew)
            {
                await _couponStore.CreateAsync(coupon);
            }
            else
            {
                await _couponStore.UpdateAsync(coupon);
            }
        }
    }

    private static CouponEditViewModel ToModel(Coupon coupon)
        => new()
        {
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
            MaxRedemptionsPerCustomer = coupon.MaxRedemptionsPerCustomer,
            MinimumAmount = coupon.MinimumAmount,
            MaximumAmount = coupon.MaximumAmount,
            MinimumQuantity = coupon.MinimumQuantity,
            BuyQuantity = coupon.Kind == CouponKind.BuyXGetY ? coupon.BuyQuantity : null,
            GetQuantity = coupon.Kind == CouponKind.BuyXGetY ? coupon.GetQuantity : null,
            MaxApplicationsPerOrder = coupon.MaxApplicationsPerOrder,
            CombinesWithProductDiscounts = coupon.CombinesWithProductDiscounts,
            CombinesWithOrderDiscounts = coupon.CombinesWithOrderDiscounts,
            CombinesWithShippingDiscounts = coupon.CombinesWithShippingDiscounts,
            Sources = coupon.Sources is { Count: > 0 } sources ? string.Join(',', sources) : null,
            ReferenceTypes = coupon.ReferenceTypes is { Count: > 0 } referenceTypes ? string.Join(',', referenceTypes) : null,
            CustomerId = coupon.CustomerId,
            CustomerEmail = coupon.CustomerEmail,
        };
}
