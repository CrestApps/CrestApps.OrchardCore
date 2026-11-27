using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

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
    private readonly IDisplayManager<MyAccountViewModel> _profileDisplayManager;
    private readonly IDisplayManager<CustomerAddressViewModel> _addressDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    internal readonly IHtmlLocalizer H;

    public MyAccountController(
        CustomerAccountPages pages,
        IDisplayManager<MyAccountViewModel> profileDisplayManager,
        IDisplayManager<CustomerAddressViewModel> addressDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IHtmlLocalizer<MyAccountController> htmlLocalizer)
    {
        _pages = pages;
        _profileDisplayManager = profileDisplayManager;
        _addressDisplayManager = addressDisplayManager;
        _updateModelAccessor = updateModelAccessor;
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

        var model = await _pages.BuildAccountAsync(customer, input: null);
        model.Editor = await _profileDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        return View(model);
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

        // The page's own values (the email, the kinds to choose from) are filled in before the editor reads the form.
        await _pages.BuildAccountAsync(customer, model);
        model.Editor = await _profileDisplayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        var result = await _pages.SaveProfileAsync(customer, model, ModelState, HttpContext.RequestAborted);

        if (result == CustomerAccountSaveResult.Invalid)
        {
            return View(model);
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

        return View("AddressEdit", await BuildAddressEditorAsync(customer, null, input: null));
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

        return View(await BuildAddressEditorAsync(customer, address, input: null));
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

        // The fields of the page are read first, so what other modules add to the editor is validated with them.
        await _addressDisplayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: addressId is null);

        var (result, entry) = await _pages.SaveAddressAsync(customer, addressId, model, ModelState, HttpContext.RequestAborted);

        if (result == CustomerAccountSaveResult.NotFound)
        {
            return NotFound();
        }

        if (result == CustomerAccountSaveResult.Invalid)
        {
            return View("AddressEdit", await BuildAddressEditorAsync(customer, entry, model));
        }

        await _pages.NotifyAsync(result, H["The address was saved."]);

        return RedirectToAction(nameof(Index));
    }

    // The address page: the customer's own address editor, built in cards.
    private async Task<CustomerAddressViewModel> BuildAddressEditorAsync(Customer customer, CustomerAddress address, CustomerAddressViewModel input)
    {
        var model = await _pages.BuildAddressEditorAsync(customer, address, input);

        model.Editor = await _addressDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: address is null);

        return model;
    }

    private Task<Customer> GetCustomerAsync()
        => _pages.GetCustomerAsync(User, HttpContext.RequestAborted);
}
