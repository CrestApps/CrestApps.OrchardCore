using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Settings > Commerce > Referrals: what a referred friend gets, and what the customer who referred them gets.
/// </summary>
public sealed class ReferralSettingsDisplayDriver : CustomerSettingsDisplayDriverBase<ReferralSettings>
{
    private readonly ICouponStore _couponStore;

    internal readonly IStringLocalizer S;

    public ReferralSettingsDisplayDriver(
        ICouponStore couponStore,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ReferralSettingsDisplayDriver> stringLocalizer)
        : base(httpContextAccessor, authorizationService)
    {
        _couponStore = couponStore;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => CustomersConstants.ReferralSettingsGroupId;

    public override IDisplayResult Edit(ISite site, ReferralSettings settings, BuildEditorContext context)
    {
        return Combine(
            Initialize<ReferralSettingsViewModel>("ReferralSettings_Edit", model => FillAsync(model, settings, includeCoupons: true))
                .Location(l => l.Zone("Content", "1").Card(S["Referred Friend"], "10"))
                .RenderWhen(CanManageCustomersAsync)
                .OnGroup(SettingsGroupId),
            Initialize<ReferralSettingsViewModel>("ReferralSettingsReward_Edit", model => FillAsync(model, settings, includeCoupons: false))
                .Location(l => l.Zone("Content", "2").Card(S["Customer Reward"], "20"))
                .RenderWhen(CanManageCustomersAsync)
                .OnGroup(SettingsGroupId));
    }

    // Only the card that lists the coupons needs them.
    private async ValueTask FillAsync(ReferralSettingsViewModel model, ReferralSettings settings, bool includeCoupons)
    {
        model.FriendCouponId = settings.FriendCouponId;
        model.RewardKind = settings.RewardKind;
        model.RewardAmount = settings.RewardAmount;
        model.RewardPoints = settings.RewardPoints;
        model.MinimumOrderAmount = settings.MinimumOrderAmount;

        if (!includeCoupons)
        {
            return;
        }

        model.Coupons = [.. (await _couponStore.GetAllAsync())
            .Where(coupon => !coupon.IsAutomatic)
            .OrderBy(coupon => coupon.Code, StringComparer.OrdinalIgnoreCase)
            .Select(coupon => new SelectListItem(string.IsNullOrEmpty(coupon.Description) ? coupon.Code : $"{coupon.Code} ({coupon.Description})", coupon.ItemId, coupon.ItemId == settings.FriendCouponId))];
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ReferralSettings settings, UpdateEditorContext context)
    {
        if (!await CanManageCustomersAsync())
        {
            return null;
        }

        var model = new ReferralSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.RewardKind == ReferralRewardKind.StoreCredit && model.RewardAmount <= 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RewardAmount), S["Enter the store credit to give, more than zero."]);
        }

        if (model.RewardKind == ReferralRewardKind.Points && model.RewardPoints < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RewardPoints), S["Enter the points to give, 1 or more."]);
        }

        if (model.MinimumOrderAmount < 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinimumOrderAmount), S["Enter 0 or more."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            settings.FriendCouponId = string.IsNullOrWhiteSpace(model.FriendCouponId) ? null : model.FriendCouponId;
            settings.RewardKind = model.RewardKind;
            settings.RewardAmount = model.RewardAmount;
            settings.RewardPoints = model.RewardPoints;
            settings.MinimumOrderAmount = model.MinimumOrderAmount;
        }

        return await EditAsync(site, settings, context);
    }
}
