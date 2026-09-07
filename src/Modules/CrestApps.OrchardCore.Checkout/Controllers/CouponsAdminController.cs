using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Checkout.Controllers;

/// <summary>
/// Manages the coupon codes a site offers.
/// </summary>
/// <remarks>
/// The two fields that get the most attention here are the usage limit and the end date, because those are
/// what stop a code that leaks from discounting indefinitely. The form nudges toward setting them, and the
/// list shows how many redemptions are left so a campaign that is running away is visible before the invoice
/// arrives.
/// </remarks>
[Admin("coupons/{action}/{itemId?}", "Coupons{action}")]
public sealed class CouponsAdminController : Controller
{
    private readonly ICouponStore _couponStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponsAdminController"/> class.
    /// </summary>
    /// <param name="couponStore">The coupon catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier used to surface outcomes.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CouponsAdminController(
        ICouponStore couponStore,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<CouponsAdminController> htmlLocalizer,
        IStringLocalizer<CouponsAdminController> stringLocalizer)
    {
        _couponStore = couponStore;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the coupons.
    /// </summary>
    [Admin("coupons", "CouponsIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CheckoutPermissions.ManageCoupons))
        {
            return Forbid();
        }

        var coupons = await _couponStore.GetAllAsync();

        return View(coupons.OrderByDescending(coupon => coupon.CreatedUtc).ToArray());
    }

    /// <summary>
    /// Displays the form for a new coupon.
    /// </summary>
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CheckoutPermissions.ManageCoupons))
        {
            return Forbid();
        }

        return View(new CouponEditViewModel { IsEnabled = true });
    }

    /// <summary>
    /// Creates a coupon.
    /// </summary>
    /// <param name="model">The submitted form.</param>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(CouponEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CheckoutPermissions.ManageCoupons))
        {
            return Forbid();
        }

        await ValidateAsync(model, existingItemId: null);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var coupon = new Coupon { ItemId = IdGenerator.GenerateId() };

        Apply(model, coupon);

        await _couponStore.CreateAsync(coupon);

        await _notifier.SuccessAsync(H["The coupon was created."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Displays the form for an existing coupon.
    /// </summary>
    /// <param name="itemId">The coupon identifier.</param>
    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CheckoutPermissions.ManageCoupons))
        {
            return Forbid();
        }

        var coupon = string.IsNullOrEmpty(itemId) ? null : await _couponStore.FindByIdAsync(itemId);

        if (coupon is null)
        {
            return NotFound();
        }

        return View(new CouponEditViewModel
        {
            ItemId = coupon.ItemId,
            Code = coupon.Code,
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
            RedemptionCount = coupon.RedemptionCount,
        });
    }

    /// <summary>
    /// Updates a coupon.
    /// </summary>
    /// <param name="itemId">The coupon identifier.</param>
    /// <param name="model">The submitted form.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, CouponEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CheckoutPermissions.ManageCoupons))
        {
            return Forbid();
        }

        var coupon = string.IsNullOrEmpty(itemId) ? null : await _couponStore.FindByIdAsync(itemId);

        if (coupon is null)
        {
            return NotFound();
        }

        await ValidateAsync(model, coupon.ItemId);

        if (!ModelState.IsValid)
        {
            model.ItemId = coupon.ItemId;
            model.RedemptionCount = coupon.RedemptionCount;

            return View(model);
        }

        Apply(model, coupon);

        await _couponStore.UpdateAsync(coupon);

        await _notifier.SuccessAsync(H["The coupon was updated."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a coupon.
    /// </summary>
    /// <param name="itemId">The coupon identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CheckoutPermissions.ManageCoupons))
        {
            return Forbid();
        }

        var coupon = string.IsNullOrEmpty(itemId) ? null : await _couponStore.FindByIdAsync(itemId);

        if (coupon is null)
        {
            return NotFound();
        }

        await _couponStore.DeleteAsync(coupon);

        await _notifier.SuccessAsync(H["The coupon was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(CouponEditViewModel model, string existingItemId)
    {
        var code = model.Code?.Trim();

        if (string.IsNullOrEmpty(code))
        {
            ModelState.AddModelError(nameof(model.Code), S["A code is required."]);
        }
        else
        {
            // Two coupons sharing a code would make which one applies a matter of query order, so the same
            // customer could get different discounts on different days.
            var existing = await _couponStore.GetByCodeAsync(code);

            if (existing is not null && !string.Equals(existing.ItemId, existingItemId, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.Code), S["That code is already in use."]);
            }
        }

        if (model.Kind == CouponKind.Percentage && model.Percentage is <= 0m or > 100m)
        {
            ModelState.AddModelError(nameof(model.Percentage), S["Enter a percentage between 1 and 100."]);
        }

        if (model.Kind == CouponKind.FixedAmount)
        {
            if (model.Amount <= 0m)
            {
                ModelState.AddModelError(nameof(model.Amount), S["Enter an amount greater than zero."]);
            }

            if (string.IsNullOrWhiteSpace(model.Currency))
            {
                // A fixed amount with no currency would apply its face value to any currency, so ten dollars
                // off would become ten thousand yen off.
                ModelState.AddModelError(nameof(model.Currency), S["A fixed amount needs a currency."]);
            }
        }

        if (model.StartsUtc.HasValue && model.EndsUtc.HasValue && model.EndsUtc.Value <= model.StartsUtc.Value)
        {
            ModelState.AddModelError(nameof(model.EndsUtc), S["The end date must be after the start date."]);
        }

        if (model.MaxRedemptions is <= 0)
        {
            ModelState.AddModelError(nameof(model.MaxRedemptions), S["Leave the limit blank for unlimited, or enter a number greater than zero."]);
        }
    }

    private static void Apply(CouponEditViewModel model, Coupon coupon)
    {
        coupon.Code = model.Code?.Trim();
        coupon.Description = model.Description?.Trim();
        coupon.Kind = model.Kind;
        coupon.Percentage = model.Kind == CouponKind.Percentage ? model.Percentage : 0m;
        coupon.Amount = model.Kind == CouponKind.FixedAmount ? model.Amount : 0m;
        coupon.Currency = model.Kind == CouponKind.FixedAmount ? model.Currency?.Trim().ToUpperInvariant() : null;
        coupon.Target = model.Target;
        coupon.IsEnabled = model.IsEnabled;
        coupon.StartsUtc = model.StartsUtc;
        coupon.EndsUtc = model.EndsUtc;
        coupon.MaxRedemptions = model.MaxRedemptions;
        coupon.MinimumAmount = model.MinimumAmount;
    }
}
