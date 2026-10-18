using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

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
    private readonly CustomerAddressBook _addressBook;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public MyAccountController(
        CustomerAccountPages pages,
        CustomerAddressBook addressBook,
        INotifier notifier,
        IHtmlLocalizer<MyAccountController> htmlLocalizer)
    {
        _pages = pages;
        _addressBook = addressBook;
        _notifier = notifier;
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

        _pages.Validate(model, ModelState);

        if (!ModelState.IsValid)
        {
            return View(await _pages.BuildAccountAsync(customer, model));
        }

        CustomerAccountPages.Apply(customer, model);

        if (await TrySaveAsync(customer))
        {
            await _notifier.SuccessAsync(H["Your details were saved."]);
        }

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

        var model = await _addressBook.BuildEditorAsync(customer, null);
        model.IsOwn = true;

        return View("AddressEdit", model);
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

        var model = await _addressBook.BuildEditorAsync(customer, address);
        model.IsOwn = true;

        return View(model);
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

        if (!customer.RemoveAddress(addressId))
        {
            return NotFound();
        }

        if (await TrySaveAsync(customer))
        {
            await _notifier.SuccessAsync(H["The address was deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAddressAsync(string addressId, CustomerAddressViewModel model)
    {
        var customer = await GetCustomerAsync();

        if (customer is null)
        {
            return Forbid();
        }

        var address = customer.FindAddress(addressId);

        if (addressId is not null && address is null)
        {
            return NotFound();
        }

        var value = await _addressBook.ValidateAsync(customer, model, address is null, ModelState);

        if (value is null)
        {
            var invalid = await _addressBook.BuildEditorAsync(customer, address, model);
            invalid.IsOwn = true;

            return View("AddressEdit", invalid);
        }

        CustomerAddressBook.Apply(customer, address, model, value);

        if (await TrySaveAsync(customer))
        {
            await _notifier.SuccessAsync(H["The address was saved."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private Task<Customer> GetCustomerAsync()
        => _pages.GetCustomerAsync(User, HttpContext.RequestAborted);

    private async Task<bool> TrySaveAsync(Customer customer)
    {
        if (await _pages.TrySaveAsync(customer, HttpContext.RequestAborted))
        {
            return true;
        }

        await _notifier.ErrorAsync(H["Your details were changed somewhere else at the same time. Your change was not saved; try again."]);

        return false;
    }
}
