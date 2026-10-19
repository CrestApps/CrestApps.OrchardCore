using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// The signed-in customer's loyalty points on their storefront account: what they have, what they're worth, and how
/// they changed.
/// </summary>
[Authorize]
[Feature(CustomersConstants.Features.Loyalty)]
public sealed class LoyaltyAccountController : Controller
{
    private readonly CustomerAccountPages _pages;
    private readonly ILoyaltyStore _store;
    private readonly LoyaltyService _loyalty;
    private readonly ISiteService _siteService;
    private readonly IShapeFactory _shapeFactory;

    public LoyaltyAccountController(
        CustomerAccountPages pages,
        ILoyaltyStore store,
        LoyaltyService loyalty,
        ISiteService siteService,
        IShapeFactory shapeFactory)
    {
        _pages = pages;
        _store = store;
        _loyalty = loyalty;
        _siteService = siteService;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("account/rewards", Name = "AccountRewards")]
    public async Task<IActionResult> Index()
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        var account = await _store.FindByCustomerAsync(customer.ItemId, HttpContext.RequestAborted);
        var settings = await _loyalty.GetSettingsAsync();
        var currency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        return View(await _shapeFactory.CreateAsync<CustomerLoyaltyViewModel>("AccountRewards", model =>
        {
            model.CustomerId = customer.ItemId;
            model.Account = account;
            model.Settings = settings;
            model.Currency = currency;
            model.HasAccount = true;
        }));
    }
}
