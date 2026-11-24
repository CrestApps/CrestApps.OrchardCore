using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.B2B.ViewModels;
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

namespace CrestApps.OrchardCore.Accounts.B2B.Controllers;

/// <summary>
/// Commerce › Price Lists: the prices accounts buy at, chosen on each account.
/// </summary>
[Admin("b2b/price-lists/{action}/{itemId?}", "PriceLists{action}")]
public sealed class PriceListsAdminController : Controller
{
    public const int MaxEntries = PriceListRules.MaxEntries;

    private readonly IPriceListStore _priceLists;
    private readonly IB2BAccountStore _accounts;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PriceListsAdminController(
        IPriceListStore priceLists,
        IB2BAccountStore accounts,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<PriceListsAdminController> htmlLocalizer,
        IStringLocalizer<PriceListsAdminController> stringLocalizer)
    {
        _priceLists = priceLists;
        _accounts = accounts;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("b2b/price-lists", "PriceLists")]
    public async Task<IActionResult> Index(string q, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, B2BAccountsPermissions.ManageB2BAccounts))
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
        if (!await _authorizationService.AuthorizeAsync(User, B2BAccountsPermissions.ManageB2BAccounts))
        {
            return Forbid();
        }

        return View("Edit", await PopulateAsync(new PriceListEditViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(PriceListEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, B2BAccountsPermissions.ManageB2BAccounts))
        {
            return Forbid();
        }

        var list = new PriceList { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow };

        if (!await ApplyAsync(model, list))
        {
            return View("Edit", await PopulateAsync(model));
        }

        await _priceLists.CreateAsync(list);
        await _notifier.SuccessAsync(H["The price list was added. Choose it on the accounts that buy at it."]);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, B2BAccountsPermissions.ManageB2BAccounts))
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
        if (!await _authorizationService.AuthorizeAsync(User, B2BAccountsPermissions.ManageB2BAccounts))
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
        if (!await _authorizationService.AuthorizeAsync(User, B2BAccountsPermissions.ManageB2BAccounts))
        {
            return Forbid();
        }

        if (await _priceLists.FindByIdAsync(itemId) is not { } list)
        {
            return NotFound();
        }

        // The accounts on it go back to the store's prices.
        await PriceListRules.DeleteAsync(_priceLists, _accounts, list, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The price list was deleted. The accounts on it pay the store's prices."]);

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

            // A later line for the same SKU replaces the earlier one.
            if (!decimal.TryParse(amount, NumberStyles.Number & ~NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var price) ||
                !PriceListRules.TryAddEntry(entries, sku, price))
            {
                unread.Add(line);
            }
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
        var (entries, unread) = ParseEntries(model.Prices);
        var problems = PriceListRules.Check(model.Name, model.PercentOff, entries.Count, out var percentOff);

        if (problems.Contains(PriceListProblem.NameRequired))
        {
            ModelState.AddModelError(nameof(model.Name), S["Name the price list."]);
        }

        if (problems.Contains(PriceListProblem.PercentOffOutOfRange))
        {
            ModelState.AddModelError(nameof(model.PercentOff), S["Enter a percentage from 0 to 100, or leave it blank."]);
        }

        if (unread.Count > 0)
        {
            ModelState.AddModelError(nameof(model.Prices), S["These lines aren't a SKU and a price: {0}", string.Join(" | ", unread.Take(5))]);
        }

        if (problems.Contains(PriceListProblem.TooManyEntries))
        {
            ModelState.AddModelError(nameof(model.Prices), S["A price list has at most {0} prices.", MaxEntries]);
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        PriceListRules.Apply(list, model.Name, percentOff, entries, (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency);

        return true;
    }

    private async Task<PriceListEditViewModel> PopulateAsync(PriceListEditViewModel model, string itemId = null)
    {
        model.ItemId = itemId;
        model.StoreCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        return model;
    }
}
