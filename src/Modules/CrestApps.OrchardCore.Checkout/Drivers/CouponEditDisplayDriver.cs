using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Checkout.Drivers;

/// <summary>
/// Builds the editor of a coupon's own fields, grouped in cards by their placement. What other modules add to a
/// coupon is built by their drivers of the coupon itself.
/// </summary>
internal sealed class CouponEditDisplayDriver : DisplayDriver<CouponEditViewModel>
{
    internal readonly IStringLocalizer S;

    public CouponEditDisplayDriver(IStringLocalizer<CouponEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CouponEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CouponEditViewModel>("CouponGeneral_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Coupon"], "10")),
            Initialize<CouponEditViewModel>("CouponDiscount_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Discount"], "20")),
            Initialize<CouponEditViewModel>("CouponConditions_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Conditions"], "30")),
            Initialize<CouponEditViewModel>("CouponAvailability_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Availability"], "40")),
            Initialize<CouponEditViewModel>("CouponCombinations_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "5").Card(S["Combines With"], "50")),
            Initialize<CouponEditViewModel>("CouponAudience_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "6").Card(S["Who and What It Is For"], "60")));
    }

    public override async Task<IDisplayResult> UpdateAsync(CouponEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Method,
            m => m.Code,
            m => m.OnlyGeneratedCodes,
            m => m.Description,
            m => m.Kind,
            m => m.Percentage,
            m => m.Amount,
            m => m.Currency,
            m => m.BuyQuantity,
            m => m.GetQuantity,
            m => m.MaxApplicationsPerOrder,
            m => m.Target,
            m => m.MinimumAmount,
            m => m.MaximumAmount,
            m => m.MinimumQuantity,
            m => m.MaxRedemptions,
            m => m.MaxRedemptionsPerCustomer,
            m => m.Starts,
            m => m.Ends,
            m => m.IsEnabled,
            m => m.CombinesWithProductDiscounts,
            m => m.CombinesWithOrderDiscounts,
            m => m.CombinesWithShippingDiscounts,
            m => m.Sources,
            m => m.ReferenceTypes,
            m => m.CustomerId,
            m => m.CustomerEmail);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the page script selects.
    protected override void BuildPrefix(CouponEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CouponEditViewModel source, CouponEditViewModel model)
    {
        model.ItemId = source.ItemId;
        model.Currencies = source.Currencies;
        model.KnownSources = source.KnownSources;
        model.Method = source.Method;
        model.Code = source.Code;
        model.OnlyGeneratedCodes = source.OnlyGeneratedCodes;
        model.Description = source.Description;
        model.Kind = source.Kind;
        model.Percentage = source.Percentage;
        model.Amount = source.Amount;
        model.Currency = source.Currency;
        model.BuyQuantity = source.BuyQuantity;
        model.GetQuantity = source.GetQuantity;
        model.MaxApplicationsPerOrder = source.MaxApplicationsPerOrder;
        model.Target = source.Target;
        model.MinimumAmount = source.MinimumAmount;
        model.MaximumAmount = source.MaximumAmount;
        model.MinimumQuantity = source.MinimumQuantity;
        model.MaxRedemptions = source.MaxRedemptions;
        model.MaxRedemptionsPerCustomer = source.MaxRedemptionsPerCustomer;
        model.StartsUtc = source.StartsUtc;
        model.EndsUtc = source.EndsUtc;
        model.Starts = source.Starts;
        model.Ends = source.Ends;
        model.IsEnabled = source.IsEnabled;
        model.CombinesWithProductDiscounts = source.CombinesWithProductDiscounts;
        model.CombinesWithOrderDiscounts = source.CombinesWithOrderDiscounts;
        model.CombinesWithShippingDiscounts = source.CombinesWithShippingDiscounts;
        model.Sources = source.Sources;
        model.ReferenceTypes = source.ReferenceTypes;
        model.CustomerId = source.CustomerId;
        model.CustomerEmail = source.CustomerEmail;
    }
}
