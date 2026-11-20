using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.ExchangeRates.Core;
using CrestApps.OrchardCore.ExchangeRates.Models;
using CrestApps.OrchardCore.ExchangeRates.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ExchangeRates.Controllers;

/// <summary>
/// Lists, enters, changes and deletes the exchange rates.
/// </summary>
[Admin]
public sealed class ExchangeRatesController : CatalogAdminController<ExchangeRate>
{
    public ExchangeRatesController(
        IExchangeRateManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ExchangeRate> displayManager,
        INotifier notifier,
        IHtmlLocalizer<ExchangeRatesController> htmlLocalizer,
        IStringLocalizer<ExchangeRatesController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission => ExchangeRatesPermissions.ManageExchangeRates;

    protected override LocalizedString CreateDisplayName => S["Exchange rate"];

    protected override LocalizedString NewDisplayName => S["New exchange rate"];

    protected override LocalizedHtmlString CreatedNotification => H["The rate was added. Documents issued from its day on use it."];

    protected override LocalizedHtmlString UpdatedNotification => H["The rate was updated. Documents issued already keep the rate they were issued with."];

    protected override LocalizedHtmlString DeletedNotification => H["The rate was deleted. Documents issued already keep the rate they were issued with."];

    protected override string GetName(ExchangeRate entry)
        => $"{entry.Currency} → {entry.BaseCurrency} {entry.Date:yyyy-MM-dd}";

    protected override void SetName(ExchangeRate entry, string name)
    {
    }

    // The newest day first, then by currency.
    protected override IEnumerable<ExchangeRate> OrderAll(IEnumerable<ExchangeRate> entries)
        => entries.OrderByDescending(entry => entry.Date).ThenBy(entry => entry.Currency, StringComparer.Ordinal);

    [Admin("commerce/exchange-rates", "ExchangeRatesIndex")]
    public Task<IActionResult> Index(CatalogEntryOptions options, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("commerce/exchange-rates", "ExchangeRatesIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    [Admin("commerce/exchange-rates/create", "ExchangeRatesCreate")]
    public Task<IActionResult> Create() => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("commerce/exchange-rates/create", "ExchangeRatesCreate")]
    public Task<IActionResult> CreatePost() => CreatePostAsync();

    [Admin("commerce/exchange-rates/edit/{id}", "ExchangeRatesEdit")]
    public Task<IActionResult> Edit(string id) => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("commerce/exchange-rates/edit/{id}", "ExchangeRatesEdit")]
    public Task<IActionResult> EditPost(string id) => EditPostAsync(id);

    [HttpPost]
    [Admin("commerce/exchange-rates/delete/{id}", "ExchangeRatesDelete")]
    public Task<IActionResult> Delete(string id) => DeleteAsync(id);
}
