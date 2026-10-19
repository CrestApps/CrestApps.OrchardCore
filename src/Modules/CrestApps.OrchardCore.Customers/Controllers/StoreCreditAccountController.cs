using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// The signed-in customer's store credit on their storefront account: what they have and how it changed.
/// </summary>
[Authorize]
[Feature(CustomersConstants.Features.StoreCredit)]
public sealed class StoreCreditAccountController : Controller
{
    private readonly CustomerAccountPages _pages;
    private readonly IStoreCreditStore _store;
    private readonly IShapeFactory _shapeFactory;

    public StoreCreditAccountController(
        CustomerAccountPages pages,
        IStoreCreditStore store,
        IShapeFactory shapeFactory)
    {
        _pages = pages;
        _store = store;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("account/store-credit", Name = "AccountStoreCredit")]
    public async Task<IActionResult> Index()
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        var accounts = await _store.ListByCustomerAsync(customer.ItemId, HttpContext.RequestAborted);

        return View(await _shapeFactory.CreateAsync<CustomerStoreCreditViewModel>("AccountStoreCredit", model =>
        {
            model.CustomerId = customer.ItemId;
            model.Accounts = accounts;
            model.HasAccount = true;
        }));
    }
}
