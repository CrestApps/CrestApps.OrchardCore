using System.Globalization;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Marketing.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.Controllers;

/// <summary>
/// Commerce › Segments: groups of customers by what they've bought, for campaigns.
/// </summary>
[Admin("marketing/segments/{action}/{itemId?}", "Segments{action}")]
public sealed class SegmentsAdminController : Controller
{
    private readonly ISegmentStore _segments;
    private readonly CampaignService _campaigns;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public SegmentsAdminController(
        ISegmentStore segments,
        CampaignService campaigns,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<SegmentsAdminController> htmlLocalizer,
        IStringLocalizer<SegmentsAdminController> stringLocalizer)
    {
        _segments = segments;
        _campaigns = campaigns;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("marketing/segments", "Segments")]
    public async Task<IActionResult> Index(string q, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, segments) = await _segments.PageAsync(q, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        return View(new SegmentsIndexViewModel
        {
            Search = q,
            List = new AdminListContext<Segment>
            {
                Source = segments,
                Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        return View("Edit", await PopulateAsync(new SegmentEditViewModel(), segment: null));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(SegmentEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        var segment = new Segment { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow };

        if (!Apply(model, segment))
        {
            return View("Edit", await PopulateAsync(model, segment: null));
        }

        await _segments.CreateAsync(segment);
        await _notifier.SuccessAsync(H["The segment was added."]);

        return RedirectToAction(nameof(Edit), new { itemId = segment.ItemId });
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        if (await _segments.FindByIdAsync(itemId) is not { } segment)
        {
            return NotFound();
        }

        var rules = segment.Rules ?? new SegmentRules();

        return View(await PopulateAsync(new SegmentEditViewModel
        {
            Name = segment.Name,
            Description = segment.Description,
            MinOrders = rules.MinOrders,
            MaxOrders = rules.MaxOrders,
            MinSpent = rules.MinSpent?.ToString(CultureInfo.InvariantCulture),
            MaxSpent = rules.MaxSpent?.ToString(CultureInfo.InvariantCulture),
            LastOrderAtLeastDaysAgo = rules.LastOrderAtLeastDaysAgo,
            LastOrderWithinDays = rules.LastOrderWithinDays,
            FirstOrderWithinDays = rules.FirstOrderWithinDays,
            Country = rules.Country,
        }, segment));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, SegmentEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        if (await _segments.FindByIdAsync(itemId) is not { } segment)
        {
            return NotFound();
        }

        if (!Apply(model, segment))
        {
            return View(await PopulateAsync(model, segment));
        }

        segment.ModifiedUtc = _clock.UtcNow;
        await _segments.UpdateAsync(segment);
        await _notifier.SuccessAsync(H["The segment was saved."]);

        return RedirectToAction(nameof(Edit), new { itemId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketingPermissions.ManageMarketing))
        {
            return Forbid();
        }

        if (await _segments.FindByIdAsync(itemId) is not { } segment)
        {
            return NotFound();
        }

        await _segments.DeleteAsync(segment);
        await _notifier.SuccessAsync(H["The segment was deleted. Campaigns sent to it are kept."]);

        return RedirectToAction(nameof(Index));
    }

    private bool Apply(SegmentEditViewModel model, Segment segment)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Name the segment, such as Lapsed buyers."]);
        }

        var minSpent = ParseAmount(model.MinSpent, nameof(model.MinSpent));
        var maxSpent = ParseAmount(model.MaxSpent, nameof(model.MaxSpent));

        if (model.MinOrders is < 0 || model.MaxOrders is < 0 || model.LastOrderAtLeastDaysAgo is < 0 or > 36_500 ||
            model.LastOrderWithinDays is < 0 or > 36_500 || model.FirstOrderWithinDays is < 0 or > 36_500)
        {
            ModelState.AddModelError(string.Empty, S["Counts and days can't be negative."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Country) && !CountryCodeUtility.IsCountryCode(model.Country))
        {
            ModelState.AddModelError(nameof(model.Country), S["Enter the country's two-letter code, such as DE, or leave it blank."]);
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        segment.Name = model.Name.Trim();
        segment.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        segment.Rules = new SegmentRules
        {
            MinOrders = model.MinOrders,
            MaxOrders = model.MaxOrders,
            MinSpent = minSpent,
            MaxSpent = maxSpent,
            LastOrderAtLeastDaysAgo = model.LastOrderAtLeastDaysAgo,
            LastOrderWithinDays = model.LastOrderWithinDays,
            FirstOrderWithinDays = model.FirstOrderWithinDays,
            Country = string.IsNullOrWhiteSpace(model.Country) ? null : model.Country.Trim().ToUpperInvariant(),
        };

        return true;
    }

    private decimal? ParseAmount(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0m)
        {
            return amount;
        }

        ModelState.AddModelError(field, S["Enter an amount, such as 100, or leave it blank."]);

        return null;
    }

    private async Task<SegmentEditViewModel> PopulateAsync(SegmentEditViewModel model, Segment segment)
    {
        model.ItemId = segment?.ItemId;
        model.StoreCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        if (segment is not null)
        {
            (model.Count, model.Reachable) = await _campaigns.CountAsync(segment, HttpContext.RequestAborted);
        }

        return model;
    }
}
