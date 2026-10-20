using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Companies.ViewModels;
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

namespace CrestApps.OrchardCore.Companies.Controllers;

/// <summary>
/// Commerce › Price lists: the prices companies buy at, chosen on each company.
/// </summary>
[Admin("companies/price-lists/{action}/{itemId?}", "PriceLists{action}")]
public sealed class PriceListsAdminController : Controller
{
    public const int MaxEntries = 5000;

    private readonly IPriceListStore _priceLists;
    private readonly ICompanyStore _companies;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PriceListsAdminController(
        IPriceListStore priceLists,
        ICompanyStore companies,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<PriceListsAdminController> htmlLocalizer,
        IStringLocalizer<PriceListsAdminController> stringLocalizer)
    {
        _priceLists = priceLists;
        _companies = companies;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("companies/price-lists", "PriceLists")]
    public async Task<IActionResult> Index(string q, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, lists) = await _priceLists.PageAsync(q, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        var pagerShape = await shapeFactory.PagerAsync(pager, count, routeData);

        return View(new PriceListsIndexViewModel
        {
            Search = q,
            List = new AdminListContext<PriceList>
            {
                Source = lists,
                Pager = (IShape)pagerShape,
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        return View("Edit", await PopulateAsync(new PriceListEditViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(PriceListEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        var list = new PriceList { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow };

        if (!await ApplyAsync(model, list))
        {
            return View("Edit", await PopulateAsync(model));
        }

        await _priceLists.CreateAsync(list);
        await _notifier.SuccessAsync(H["The price list was added. Choose it on the companies that buy at it."]);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        if (await _priceLists.FindByIdAsync(itemId) is not { } list)
        {
            return NotFound();
        }

        return View(await PopulateAsync(new PriceListEditViewModel
        {
            Name = list.Name,
            PercentOff = list.PercentOff?.ToString(CultureInfo.InvariantCulture),
            Prices = FormatEntries(list.Entries),
        }, list.ItemId));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, PriceListEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        if (await _priceLists.FindByIdAsync(itemId) is not { } list)
        {
            return NotFound();
        }

        if (!await ApplyAsync(model, list))
        {
            return View(await PopulateAsync(model, list.ItemId));
        }

        list.ModifiedUtc = _clock.UtcNow;
        await _priceLists.UpdateAsync(list);
        await _notifier.SuccessAsync(H["The price list was saved."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CompaniesPermissions.ManageCompanies))
        {
            return Forbid();
        }

        if (await _priceLists.FindByIdAsync(itemId) is not { } list)
        {
            return NotFound();
        }

        // The companies on it go back to the store's prices.
        var (_, companies) = await _companies.PageAsync(search: null, 0, int.MaxValue, HttpContext.RequestAborted);

        foreach (var company in companies.Where(company => company.PriceListId == list.ItemId))
        {
            company.PriceListId = null;
            await _companies.UpdateAsync(company);
        }

        await _priceLists.DeleteAsync(list);
        await _notifier.SuccessAsync(H["The price list was deleted. The companies on it pay the store's prices."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Reads the prices by SKU from their text: a SKU and a price on each line, apart by a comma, semicolon or tab, such
    /// as pasted from a spreadsheet. A price reads with a decimal point, or a decimal comma when the SKU is set apart by
    /// a semicolon or tab. Returns the entries, and the lines that couldn't be read.
    /// </summary>
    public static (List<PriceListEntry> Entries, List<string> Unread) ParseEntries(string text)
    {
        var entries = new List<PriceListEntry>();
        var unread = new List<string>();

        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var separator = line.IndexOfAny([';', '\t']);
            var decimalComma = separator >= 0;

            if (separator < 0)
            {
                separator = line.IndexOf(',');
            }

            var sku = separator > 0 ? line[..separator].Trim().Trim('"') : null;
            var amount = separator > 0 ? line[(separator + 1)..].Trim().Trim('"') : null;

            if (decimalComma && amount is not null)
            {
                amount = amount.Replace(',', '.');
            }

            if (string.IsNullOrEmpty(sku) || sku.Length > 100 ||
                !decimal.TryParse(amount, NumberStyles.Number & ~NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var price) || price < 0m)
            {
                unread.Add(line);

                continue;
            }

            // A later line for the same SKU replaces the earlier one.
            entries.RemoveAll(entry => string.Equals(entry.Sku, sku, StringComparison.OrdinalIgnoreCase));
            entries.Add(new PriceListEntry { Sku = sku, UnitPrice = price });
        }

        return (entries, unread);
    }

    public static string FormatEntries(IEnumerable<PriceListEntry> entries)
    {
        var text = new StringBuilder();

        foreach (var entry in entries ?? [])
        {
            text.Append(entry.Sku).Append(", ").Append(entry.UnitPrice.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return text.ToString();
    }

    private async Task<bool> ApplyAsync(PriceListEditViewModel model, PriceList list)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Name the price list."]);
        }

        decimal? percentOff = null;

        if (!string.IsNullOrWhiteSpace(model.PercentOff))
        {
            if (!decimal.TryParse(model.PercentOff, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) || percent is < 0m or > 100m)
            {
                ModelState.AddModelError(nameof(model.PercentOff), S["Enter a percentage from 0 to 100, or leave it blank."]);
            }
            else if (percent > 0m)
            {
                percentOff = percent;
            }
        }

        var (entries, unread) = ParseEntries(model.Prices);

        if (unread.Count > 0)
        {
            ModelState.AddModelError(nameof(model.Prices), S["These lines aren't a SKU and a price: {0}", string.Join(" | ", unread.Take(5))]);
        }

        if (entries.Count > MaxEntries)
        {
            ModelState.AddModelError(nameof(model.Prices), S["A price list has at most {0} prices.", MaxEntries]);
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        list.Name = model.Name.Trim();
        list.PercentOff = percentOff;
        list.Entries = entries;
        list.Currency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        return true;
    }

    private async Task<PriceListEditViewModel> PopulateAsync(PriceListEditViewModel model, string itemId = null)
    {
        model.ItemId = itemId;
        model.StoreCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        return model;
    }
}
