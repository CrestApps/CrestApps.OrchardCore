using System.Text.RegularExpressions;
using CrestApps.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.MailingLists.WelcomeCoupon;

/// <summary>
/// The Welcome coupon card of the mailing list editor: the template coupon, the code prefix, how long a code works,
/// when it is issued and who delivers it, kept in the list's properties.
/// </summary>
internal sealed partial class MailingListWelcomeCouponDisplayDriver : DisplayDriver<MailingList>
{
    private const int MaxValidForDays = 3_650;

    private readonly ICouponStore _coupons;

    internal readonly IStringLocalizer S;

    public MailingListWelcomeCouponDisplayDriver(
        ICouponStore coupons,
        IStringLocalizer<MailingListWelcomeCouponDisplayDriver> stringLocalizer)
    {
        _coupons = coupons;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gives the card its own field prefix, so its fields (such as <c>IsEnabled</c>) never share a name with the list's own.
    /// </summary>
    /// <param name="model">The list being edited.</param>
    /// <param name="htmlFieldPrefix">The prefix of the editor.</param>
    protected override void BuildPrefix(MailingList model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "WelcomeCoupon" : $"{htmlFieldPrefix}.WelcomeCoupon";
    }

    public override IDisplayResult Edit(MailingList list, BuildEditorContext context)
        => Initialize<MailingListWelcomeCouponViewModel>("MailingListWelcomeCoupon_Edit", async model =>
        {
            var settings = list.TryGet<MailingListWelcomeCoupon>(out var kept) && kept is not null ? kept : new MailingListWelcomeCoupon();

            model.IsEnabled = settings.IsEnabled;
            model.CouponId = settings.CouponId;
            model.Prefix = settings.Prefix;
            model.ValidForDays = settings.ValidForDays;
            model.Timing = settings.Timing;
            model.Delivery = settings.Delivery;
            model.Coupons = [.. (await _coupons.GetAllAsync())
                .Where(coupon => !coupon.IsAutomatic)
                .OrderBy(coupon => coupon.Code, StringComparer.OrdinalIgnoreCase)
                .Select(coupon => new SelectListItem(
                    string.IsNullOrWhiteSpace(coupon.Description) ? coupon.Code : $"{coupon.Code} ({coupon.Description})",
                    coupon.ItemId,
                    coupon.ItemId == settings.CouponId))];
        }).Location("Content:20");

    public override async Task<IDisplayResult> UpdateAsync(MailingList list, UpdateEditorContext context)
    {
        var model = new MailingListWelcomeCouponViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var prefix = string.IsNullOrWhiteSpace(model.Prefix) ? null : model.Prefix.Trim().ToUpperInvariant();

        if (model.IsEnabled)
        {
            if (string.IsNullOrWhiteSpace(model.CouponId) || await _coupons.FindByIdAsync(model.CouponId) is not { IsAutomatic: false })
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.CouponId), S["Choose the coupon the welcome codes are issued from."]);
            }

            if (prefix is not null && (prefix.Length > CheckoutConstants.MaxCouponCodePrefixLength || !PrefixPattern().IsMatch(prefix)))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Prefix), S["The prefix holds up to {0} letters, digits and dashes.", CheckoutConstants.MaxCouponCodePrefixLength]);
            }

            if (model.ValidForDays is < 1 or > MaxValidForDays)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.ValidForDays), S["A code works between 1 and {0} days, or leave it empty to follow the coupon's end.", MaxValidForDays]);
            }
        }

        list.Put(new MailingListWelcomeCoupon
        {
            IsEnabled = model.IsEnabled,
            CouponId = string.IsNullOrWhiteSpace(model.CouponId) ? null : model.CouponId,
            Prefix = prefix,
            ValidForDays = model.ValidForDays,
            Timing = Enum.IsDefined(model.Timing) ? model.Timing : MailingListCouponTiming.AfterVerification,
            Delivery = Enum.IsDefined(model.Delivery) ? model.Delivery : MailingListCouponDelivery.Email,
        });

        return Edit(list, context);
    }

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex PrefixPattern();
}
