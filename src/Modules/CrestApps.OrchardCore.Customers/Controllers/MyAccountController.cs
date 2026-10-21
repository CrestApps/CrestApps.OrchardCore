using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// A signed-in customer's own profile and address book.
/// </summary>
/// <remarks>
/// The account's sign-in, password and email stay with Orchard Core Users; this is what the store keeps besides.
/// </remarks>
[CustomerAdminPage]
public sealed class MyAccountController : Controller
{
    private readonly CustomerAccountPages _pages;

    internal readonly IHtmlLocalizer H;

    public MyAccountController(
        CustomerAccountPages pages,
        IHtmlLocalizer<MyAccountController> htmlLocalizer)
    {
        _pages = pages;
        H = htmlLocalizer;
    }

    [Admin("my-account", "MyAccount")]
    public async Task<IActionResult> Index()
    {
        var customer = await GetCustomerAsync();

        if (customer is null)
        {
            return Forbid();
        }

        return View(await _pages.BuildAccountAsync(customer, input: null));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [Admin("my-account", "MyAccount")]
    public async Task<IActionResult> IndexPost(MyAccountViewModel model)
    {
        var customer = await GetCustomerAsync();

        if (customer is null)
        {
            return Forbid();
        }

        var result = await _pages.SaveProfileAsync(customer, model, ModelState, HttpContext.RequestAborted);

        if (result == CustomerAccountSaveResult.Invalid)
        {
            return View(await _pages.BuildAccountAsync(customer, model));
        }

        await _pages.NotifyAsync(result, H["Your details were saved."]);

        return RedirectToAction(nameof(Index));
    }

    [Admin("my-account/addresses/add", "MyAccountAddressCreate")]
    public async Task<IActionResult> AddressCreate()
    {
        var customer = await GetCustomerAsync();

        if (customer is null)
        {
            return Forbid();
        }

        return View("AddressEdit", await _pages.BuildAddressEditorAsync(customer, null));
    }

    [HttpPost]
    [ActionName(nameof(AddressCreate))]
    [Admin("my-account/addresses/add", "MyAccountAddressCreate")]
    public Task<IActionResult> AddressCreatePost(CustomerAddressViewModel model)
        => SaveAddressAsync(addressId: null, model);

    [Admin("my-account/addresses/{addressId}", "MyAccountAddressEdit")]
    public async Task<IActionResult> AddressEdit(string addressId)
    {
        var customer = await GetCustomerAsync();

        if (customer is null)
        {
            return Forbid();
        }

        var address = customer.FindAddress(addressId);

        if (address is null)
        {
            return NotFound();
        }

        return View(await _pages.BuildAddressEditorAsync(customer, address));
    }

    [HttpPost]
    [ActionName(nameof(AddressEdit))]
    [Admin("my-account/addresses/{addressId}", "MyAccountAddressEdit")]
    public Task<IActionResult> AddressEditPost(string addressId, CustomerAddressViewModel model)
        => SaveAddressAsync(addressId, model);

    [HttpPost]
    [Admin("my-account/addresses/{addressId}/delete", "MyAccountAddressDelete")]
    public async Task<IActionResult> AddressDelete(string addressId)
    {
        var customer = await GetCustomerAsync();

        if (customer is null)
        {
            return Forbid();
        }

        var result = await _pages.DeleteAddressAsync(customer, addressId, HttpContext.RequestAborted);

        if (result == CustomerAccountSaveResult.NotFound)
        {
            return NotFound();
        }

        await _pages.NotifyAsync(result, H["The address was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAddressAsync(string addressId, CustomerAddressViewModel model)
    {
        var customer = await GetCustomerAsync();

        if (customer is null)
        {
            return Forbid();
        }

        var (result, entry) = await _pages.SaveAddressAsync(customer, addressId, model, ModelState, HttpContext.RequestAborted);

        if (result == CustomerAccountSaveResult.NotFound)
        {
            return NotFound();
        }

        if (result == CustomerAccountSaveResult.Invalid)
        {
            return View("AddressEdit", await _pages.BuildAddressEditorAsync(customer, entry, model));
        }

        await _pages.NotifyAsync(result, H["The address was saved."]);

        return RedirectToAction(nameof(Index));
    }

    private Task<Customer> GetCustomerAsync()
        => _pages.GetCustomerAsync(User, HttpContext.RequestAborted);
}
