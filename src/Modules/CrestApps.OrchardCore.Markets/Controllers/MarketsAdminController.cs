using System.Globalization;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Services;
using CrestApps.OrchardCore.Markets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Markets.Controllers;

/// <summary>
/// Commerce › Markets: the countries the store sells to, each group with its currency, rate, rounding, language and
/// tax.
/// </summary>
[Admin("markets/{action}/{itemId?}", "Markets{action}")]
public sealed class MarketsAdminController : Controller
{
    private readonly IMarketStore _markets;
    private readonly ISiteService _siteService;
    private readonly ILocalizationService _localizationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public MarketsAdminController(
        IMarketStore markets,
        ISiteService siteService,
        ILocalizationService localizationService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<MarketsAdminController> htmlLocalizer,
        IStringLocalizer<MarketsAdminController> stringLocalizer)
    {
        _markets = markets;
        _siteService = siteService;
        _localizationService = localizationService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("markets", "Markets")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketsPermissions.ManageMarkets))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, markets) = await _markets.PageAsync(pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var pagerShape = await shapeFactory.PagerAsync(pager, count, new RouteData());

        return View(new MarketsIndexViewModel
        {
            StoreCurrency = await GetStoreCurrencyAsync(),
            List = new AdminListContext<Market>
            {
                Source = markets,
                Pager = (IShape)pagerShape,
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketsPermissions.ManageMarkets))
        {
            return Forbid();
        }

        return View("Edit", await PopulateAsync(new MarketEditViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(MarketEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketsPermissions.ManageMarkets))
        {
            return Forbid();
        }

        await ValidateAsync(model, itemId: null);

        if (!ModelState.IsValid)
        {
            return View("Edit", await PopulateAsync(model));
        }

        var market = new Market { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow };

        Apply(model, market);
        await _markets.CreateAsync(market);
        await _notifier.SuccessAsync(H["The market was added. Shoppers in its countries now see prices in {0}.", market.Currency]);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketsPermissions.ManageMarkets))
        {
            return Forbid();
        }

        if (await _markets.FindByIdAsync(itemId) is not { } market)
        {
            return NotFound();
        }

        return View(await PopulateAsync(new MarketEditViewModel
        {
            Name = market.Name,
            Countries = string.Join(", ", market.Countries),
            Currency = market.Currency,
            ExchangeRate = market.ExchangeRate,
            Rounding = market.Rounding,
            Culture = market.Culture,
            PricesIncludeTax = market.PricesIncludeTax,
            IsEnabled = market.IsEnabled,
        }, market.ItemId));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, MarketEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketsPermissions.ManageMarkets))
        {
            return Forbid();
        }

        if (await _markets.FindByIdAsync(itemId) is not { } market)
        {
            return NotFound();
        }

        await ValidateAsync(model, market.ItemId);

        if (!ModelState.IsValid)
        {
            return View(await PopulateAsync(model, market.ItemId));
        }

        Apply(model, market);
        market.ModifiedUtc = _clock.UtcNow;
        await _markets.UpdateAsync(market);
        await _notifier.SuccessAsync(H["The market was saved."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketsPermissions.ManageMarkets))
        {
            return Forbid();
        }

        if (await _markets.FindByIdAsync(itemId) is not { } market)
        {
            return NotFound();
        }

        await _markets.DeleteAsync(market);
        await _notifier.SuccessAsync(H["The market was deleted. Its shoppers now see the store's own prices."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Returns the two-letter country codes in a list typed by hand, upper-cased and without repeats.
    /// </summary>
    internal static List<string> ParseCountries(string value)
        => [.. (value ?? string.Empty)
            .Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)];

    private async Task ValidateAsync(MarketEditViewModel model, string itemId)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Name the market, such as Europe or Germany."]);
        }

        var countries = ParseCountries(model.Countries);

        if (countries.Count == 0 || countries.Any(code => code.Length != 2 || !code.All(char.IsAsciiLetter)))
        {
            ModelState.AddModelError(nameof(model.Countries), S["List the market's countries by their two-letter codes, such as DE, AT."]);
        }
        else
        {
            // A country is in one market only, so a shopper's market is never a guess between two.
            var taken = (await _markets.GetAllAsync())
                .Where(other => other.ItemId != itemId)
                .SelectMany(other => other.Countries.Where(countries.Contains).Select(code => (code, other.Name)))
                .FirstOrDefault();

            if (taken.code is not null)
            {
                ModelState.AddModelError(nameof(model.Countries), S["{0} is already in the market {1}.", taken.code, taken.Name]);
            }
        }

        if (string.IsNullOrWhiteSpace(model.Currency) || model.Currency.Trim().Length != 3 || !model.Currency.Trim().All(char.IsAsciiLetter))
        {
            ModelState.AddModelError(nameof(model.Currency), S["Enter the market's three-letter currency code, such as EUR."]);
        }

        if (model.ExchangeRate <= 0m)
        {
            ModelState.AddModelError(nameof(model.ExchangeRate), S["Enter how much of the market's currency one unit of the store's currency is worth."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Culture) && !(await _localizationService.GetSupportedCulturesAsync()).Contains(model.Culture.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Culture), S["Choose one of the site's languages, or none."]);
        }
    }

    private static void Apply(MarketEditViewModel model, Market market)
    {
        market.Name = model.Name.Trim();
        market.Countries = ParseCountries(model.Countries);
        market.Currency = model.Currency.Trim().ToUpperInvariant();
        market.ExchangeRate = model.ExchangeRate;
        market.Rounding = model.Rounding;
        market.Culture = string.IsNullOrWhiteSpace(model.Culture) ? null : model.Culture.Trim();
        market.PricesIncludeTax = model.PricesIncludeTax;
        market.IsEnabled = model.IsEnabled;
    }

    private async Task<MarketEditViewModel> PopulateAsync(MarketEditViewModel model, string itemId = null)
    {
        model.ItemId = itemId;
        model.StoreCurrency = await GetStoreCurrencyAsync();
        model.Roundings =
        [
            new SelectListItem(S["To the cent (none)"], nameof(MarketPriceRounding.None), model.Rounding == MarketPriceRounding.None),
            new SelectListItem(S["Up to a whole amount, such as 24.00"], nameof(MarketPriceRounding.WholeUnits), model.Rounding == MarketPriceRounding.WholeUnits),
            new SelectListItem(S["Up to .99, such as 23.99"], nameof(MarketPriceRounding.EndIn99), model.Rounding == MarketPriceRounding.EndIn99),
            new SelectListItem(S["Up to .95, such as 23.95"], nameof(MarketPriceRounding.EndIn95), model.Rounding == MarketPriceRounding.EndIn95),
        ];
        model.Cultures =
        [
            new SelectListItem(S["Keep the shopper's language"], string.Empty, string.IsNullOrEmpty(model.Culture)),
            .. (await _localizationService.GetSupportedCulturesAsync())
                .Select(name => new SelectListItem(GetCultureName(name), name, string.Equals(name, model.Culture, StringComparison.OrdinalIgnoreCase))),
        ];

        return model;
    }

    private static string GetCultureName(string name)
    {
        try
        {
            return $"{CultureInfo.GetCultureInfo(name).DisplayName} ({name})";
        }
        catch (CultureNotFoundException)
        {
            return name;
        }
    }

    private async Task<string> GetStoreCurrencyAsync()
        => (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
}
