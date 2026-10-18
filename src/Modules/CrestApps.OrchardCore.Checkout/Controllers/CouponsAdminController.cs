using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

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
    private const string _optionsSearch = "Options.Search";
    private const string _optionsStatus = "Options.Status";

    private const int RecentUses = 25;

    private readonly ICouponStore _couponStore;
    private readonly ICouponRedemptionService _redemptions;
    private readonly IDisplayManager<Coupon> _couponDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
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
        ICouponRedemptionService redemptions,
        IDisplayManager<Coupon> couponDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<CouponsAdminController> htmlLocalizer,
        IStringLocalizer<CouponsAdminController> stringLocalizer)
    {
        _couponStore = couponStore;
        _redemptions = redemptions;
        _couponDisplayManager = couponDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the coupons.
    /// </summary>
    [Admin("coupons", "CouponsIndex")]
    /// <param name="options">The filters.</param>
    /// <param name="pagerParameters">The page to show.</param>
    /// <param name="pagerOptions">The site's pager options.</param>
    /// <param name="shapeFactory">The shape factory the pager is built with.</param>
    /// <param name="clock">The clock deciding which coupons are redeemable now.</param>
    public async Task<IActionResult> Index(
        CouponsIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory,
        [FromServices] IClock clock)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CheckoutPermissions.ManageCoupons))
        {
            return Forbid();
        }

        options ??= new CouponsIndexOptions();

        // A site has tens of coupons, not thousands, so they are filtered and paged in memory.
        var now = clock.UtcNow;
        var all = (await _couponStore.GetAllAsync()).ToArray();
        IEnumerable<Coupon> coupons = all;

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var search = options.Search.Trim();

            coupons = coupons.Where(coupon =>
                (coupon.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (coupon.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        coupons = options.Status switch
        {
            CouponStatusFilter.Active => coupons.Where(coupon => coupon.IsEnabled && coupon.IsRedeemable(now)),
            CouponStatusFilter.NotAvailable => coupons.Where(coupon => coupon.IsEnabled && !coupon.IsRedeemable(now)),
            CouponStatusFilter.Disabled => coupons.Where(coupon => !coupon.IsEnabled),
            _ => coupons,
        };

        var matching = coupons.OrderByDescending(coupon => coupon.CreatedUtc).ToArray();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status != CouponStatusFilter.All)
        {
            routeData.Values.TryAdd(_optionsStatus, options.Status);
        }

        options.Statuses =
        [
            new SelectListItem(S["Any status"], nameof(CouponStatusFilter.All), options.Status == CouponStatusFilter.All),
            new SelectListItem(S["Active"], nameof(CouponStatusFilter.Active), options.Status == CouponStatusFilter.Active),
            new SelectListItem(S["Not available"], nameof(CouponStatusFilter.NotAvailable), options.Status == CouponStatusFilter.NotAvailable),
            new SelectListItem(S["Disabled"], nameof(CouponStatusFilter.Disabled), options.Status == CouponStatusFilter.Disabled),
        ];

        var viewModel = new CouponsIndexViewModel
        {
            Options = options,
            Coupons = [.. matching.Skip(pager.GetStartIndex()).Take(pager.PageSize)],
            HasUnboundedCoupons = all.Any(coupon => coupon.IsEnabled && !coupon.MaxRedemptions.HasValue && !coupon.EndsUtc.HasValue),
            Pager = await shapeFactory.PagerAsync(pager, matching.Length, routeData),
        };

        // The admin-list tag builds a row per coupon, in the layout (List or Grid) the site or the user picked.
        viewModel.List = new AdminListContext<Coupon>
        {
            Source = viewModel.Coupons,
            Pager = (IShape)viewModel.Pager,
        };

        // The status filter renders at the end of the list's toolbar.
        viewModel.List.ToolbarActions = await shapeFactory.CreateAsync("CouponsAdminListFilters", Arguments.From(new { Options = options }));

        return View(viewModel);
    }

    /// <summary>
    /// Applies the list filters.
    /// </summary>
    /// <param name="options">The filters.</param>
    /// <param name="pagerParameters">The page size to keep while filtering.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("coupons", "CouponsIndex")]
    public async Task<IActionResult> IndexFilterPost(CouponsIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CheckoutPermissions.ManageCoupons))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeValues.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status != CouponStatusFilter.All)
        {
            routeValues.TryAdd(_optionsStatus, options.Status);
        }

        if (pagerParameters.PageSize.HasValue)
        {
            routeValues.TryAdd("pageSize", pagerParameters.PageSize.Value);
        }

        return RedirectToAction(nameof(Index), routeValues);
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

        var model = new CouponEditViewModel { IsEnabled = true };

        await PopulateCurrenciesAsync(model);
        PopulateSources(model);
        model.Editor = await _couponDisplayManager.BuildEditorAsync(new Coupon(), _updateModelAccessor.ModelUpdater, isNew: true);

        return View(model);
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

        var coupon = new Coupon { ItemId = IdGenerator.GenerateId() };

        // What other modules add, such as the products the coupon is for, is read into the coupon first; the
        // fields of this form are applied after, once everything is valid.
        var editor = await _couponDisplayManager.UpdateEditorAsync(coupon, _updateModelAccessor.ModelUpdater, isNew: true);

        await ValidateAsync(model, existingItemId: null);

        if (!ModelState.IsValid)
        {
            await PopulateCurrenciesAsync(model);
            PopulateSources(model);
            model.Editor = editor;

            return View(model);
        }

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

        var model = new CouponEditViewModel
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
            MaximumAmount = coupon.MaximumAmount,
            MaxRedemptionsPerCustomer = coupon.MaxRedemptionsPerCustomer,
            RedemptionCount = coupon.RedemptionCount,
            Sources = string.Join(", ", coupon.Sources ?? []),
            ReferenceTypes = string.Join(", ", coupon.ReferenceTypes ?? []),
            CustomerId = coupon.CustomerId,
            CustomerEmail = coupon.CustomerEmail,
        };

        await PopulateCurrenciesAsync(model);
        PopulateSources(model);
        await PopulateUsesAsync(model, coupon);
        model.Editor = await _couponDisplayManager.BuildEditorAsync(coupon, _updateModelAccessor.ModelUpdater, isNew: false);

        return View(model);
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

        var editor = await _couponDisplayManager.UpdateEditorAsync(coupon, _updateModelAccessor.ModelUpdater, isNew: false);

        await ValidateAsync(model, coupon.ItemId);

        if (!ModelState.IsValid)
        {
            model.ItemId = coupon.ItemId;
            model.RedemptionCount = coupon.RedemptionCount;

            await PopulateCurrenciesAsync(model);
            PopulateSources(model);
            await PopulateUsesAsync(model, coupon);
            model.Editor = editor;

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

        if (!string.IsNullOrWhiteSpace(model.CustomerEmail) && !model.CustomerEmail.Contains('@', StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.CustomerEmail), S["Enter the customer's email address, or leave it blank."]);
        }

        if (model.MaxRedemptions is <= 0)
        {
            ModelState.AddModelError(nameof(model.MaxRedemptions), S["Leave the limit blank for unlimited, or enter a number greater than zero."]);
        }

        if (model.MaxRedemptionsPerCustomer is <= 0)
        {
            ModelState.AddModelError(nameof(model.MaxRedemptionsPerCustomer), S["Leave the limit blank for no limit, or enter a number greater than zero."]);
        }

        if (model.MaximumAmount is <= 0m)
        {
            ModelState.AddModelError(nameof(model.MaximumAmount), S["Leave the maximum blank, or enter an amount greater than zero."]);
        }
        else if (model.MaximumAmount.HasValue && model.MinimumAmount.HasValue && model.MaximumAmount.Value < model.MinimumAmount.Value)
        {
            ModelState.AddModelError(nameof(model.MaximumAmount), S["The maximum amount can't be less than the minimum amount."]);
        }
    }

    private async Task PopulateUsesAsync(CouponEditViewModel model, Coupon coupon)
    {
        model.Redemptions = await _redemptions.ListAsync(coupon.Code, RecentUses, HttpContext.RequestAborted);
        model.HeldCount = await _redemptions.CountHeldAsync(coupon.Code, HttpContext.RequestAborted);

        // What each use paid for, such as an order, reads by its number rather than its id when a module knows it.
        var describers = HttpContext.RequestServices.GetServices<ICheckoutReferenceDescriber>().ToArray();
        var references = new Dictionary<string, CheckoutReferenceDescription>(StringComparer.Ordinal);

        foreach (var redemption in model.Redemptions.Where(redemption => !string.IsNullOrEmpty(redemption.ReferenceId)))
        {
            var key = redemption.ReferenceType + "|" + redemption.ReferenceId;

            if (references.ContainsKey(key))
            {
                continue;
            }

            foreach (var describer in describers)
            {
                if (await describer.DescribeAsync(redemption.ReferenceType, redemption.ReferenceId, HttpContext.RequestAborted) is { } description)
                {
                    references[key] = description;

                    break;
                }
            }
        }

        model.References = references;
    }

    // A typed code was easy to get wrong and a wrong one silently never matched a checkout, so the currencies the
    // site sells in are offered as a list. The list comes from Products, which is optional; without it the code is
    // still typed.
    private async Task PopulateCurrenciesAsync(CouponEditViewModel model)
    {
        if (HttpContext.RequestServices.GetService<IProductCurrencyProvider>() is not { } currencyProvider)
        {
            return;
        }

        var currencies = await currencyProvider.GetCurrenciesAsync(HttpContext.RequestAborted);

        if (currencies.Count == 0)
        {
            return;
        }

        var selected = model.Currency?.Trim().ToUpperInvariant();

        var items = currencies
            .Select(currency => new SelectListItem($"{currency.DisplayName} ({currency.CurrencyCode})", currency.CurrencyCode)
            {
                Selected = string.Equals(currency.CurrencyCode, selected, StringComparison.OrdinalIgnoreCase),
            })
            .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A coupon saved before the currency was removed from the list keeps showing what it was set to.
        if (!string.IsNullOrEmpty(selected) && !items.Any(item => item.Selected))
        {
            items.Add(new SelectListItem(selected, selected, selected: true));
        }

        model.Currencies = items;
    }

    // The sources the enabled features record on their transactions, so the editor can say which values the source
    // limit accepts. A source is a technical key, and a typo in one would quietly make a coupon unusable.
    private void PopulateSources(CouponEditViewModel model)
    {
        var sources = HttpContext.RequestServices.GetService<IOptions<TransactionSourceOptions>>()?.Value?.Sources;

        if (sources is not { Count: > 0 })
        {
            return;
        }

        model.KnownSources = sources.Values
            .Select(source => source.DisplayName is null ? source.Name : $"{source.Name} ({source.DisplayName.Value})")
            .OrderBy(text => text, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static void Apply(CouponEditViewModel model, Coupon coupon)
    {
        coupon.Code = model.Code?.Trim();
        coupon.Description = model.Description?.Trim();
        coupon.Kind = model.Kind;
        coupon.Percentage = model.Kind == CouponKind.Percentage ? model.Percentage : 0m;
        coupon.Amount = model.Kind == CouponKind.FixedAmount ? model.Amount : 0m;
        coupon.Currency = model.Kind == CouponKind.FixedAmount ? model.Currency?.Trim().ToUpperInvariant() : null;
        // Free shipping takes off shipping, whatever it was set to apply to before.
        coupon.Target = model.Kind == CouponKind.FreeShipping ? DiscountTarget.OneTime : model.Target;
        coupon.IsEnabled = model.IsEnabled;
        coupon.StartsUtc = model.StartsUtc;
        coupon.EndsUtc = model.EndsUtc;
        coupon.MaxRedemptions = model.MaxRedemptions;
        coupon.MinimumAmount = model.MinimumAmount;
        coupon.MaximumAmount = model.MaximumAmount;
        coupon.MaxRedemptionsPerCustomer = model.MaxRedemptionsPerCustomer;
        coupon.Sources = SplitList(model.Sources);
        coupon.ReferenceTypes = SplitList(model.ReferenceTypes);
        coupon.CustomerId = TrimToNull(model.CustomerId);
        coupon.CustomerEmail = TrimToNull(model.CustomerEmail);
    }
}
