using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// The signed-in customer's details and address book on the storefront account. Each page's content is a shape
/// (<c>AccountDetails</c>, <c>AccountAddresses</c>, <c>AccountAddressEdit</c>) that a theme can override.
/// </summary>
[Authorize]
public sealed class AccountProfileController : Controller
{
    private readonly CustomerAccountPages _pages;
    private readonly CustomerAddressBook _addressBook;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;

    public AccountProfileController(
        CustomerAccountPages pages,
        CustomerAddressBook addressBook,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<AccountProfileController> htmlLocalizer)
    {
        _pages = pages;
        _addressBook = addressBook;
        _shapeFactory = shapeFactory;
        H = htmlLocalizer;
    }

    [HttpGet("account/details", Name = "AccountDetails")]
    public async Task<IActionResult> Details()
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        return View(await DetailsShapeAsync(await _pages.BuildAccountAsync(customer, input: null)));
    }

    [HttpPost("account/details")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(MyAccountViewModel model)
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        var result = await _pages.SaveProfileAsync(customer, model, ModelState, HttpContext.RequestAborted);

        if (result == CustomerAccountSaveResult.Invalid)
        {
            return View(await DetailsShapeAsync(await _pages.BuildAccountAsync(customer, model)));
        }

        await _pages.NotifyAsync(result, H["Your details were saved."]);

        return RedirectToAction(nameof(Details));
    }

    [HttpGet("account/addresses", Name = "AccountAddresses")]
    public async Task<IActionResult> Addresses()
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        var addresses = await _addressBook.SummarizeAsync(customer);

        return View(await _shapeFactory.CreateAsync<CustomerAddressListViewModel>("AccountAddresses", model =>
        {
            model.Addresses = addresses;
            model.IsOwn = true;
        }));
    }

    [HttpGet("account/addresses/add", Name = "AccountAddressCreate")]
    public async Task<IActionResult> AddressCreate()
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        return View("AddressEdit", await AddressShapeAsync(await _pages.BuildAddressEditorAsync(customer, null)));
    }

    [HttpPost("account/addresses/add")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddressCreate(CustomerAddressViewModel model)
        => SaveAddressAsync(addressId: null, model);

    [HttpGet("account/addresses/{addressId}", Name = "AccountAddressEdit")]
    public async Task<IActionResult> AddressEdit(string addressId)
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        if (customer.FindAddress(addressId) is not { } address)
        {
            return NotFound();
        }

        return View(await AddressShapeAsync(await _pages.BuildAddressEditorAsync(customer, address)));
    }

    [HttpPost("account/addresses/{addressId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddressEdit(string addressId, CustomerAddressViewModel model)
        => SaveAddressAsync(addressId, model);

    [HttpPost("account/addresses/{addressId}/delete", Name = "AccountAddressDelete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddressDelete(string addressId)
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        var result = await _pages.DeleteAddressAsync(customer, addressId, HttpContext.RequestAborted);

        if (result == CustomerAccountSaveResult.NotFound)
        {
            return NotFound();
        }

        await _pages.NotifyAsync(result, H["The address was deleted."]);

        return RedirectToAction(nameof(Addresses));
    }

    private async Task<IActionResult> SaveAddressAsync(string addressId, CustomerAddressViewModel model)
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
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
            return View("AddressEdit", await AddressShapeAsync(await _pages.BuildAddressEditorAsync(customer, entry, model)));
        }

        await _pages.NotifyAsync(result, H["The address was saved."]);

        return RedirectToAction(nameof(Addresses));
    }

    // The shape's model is a proxy Orchard Core derives from the view model, so the built page is copied onto it.
    private ValueTask<IShape> DetailsShapeAsync(MyAccountViewModel source)
        => _shapeFactory.CreateAsync<MyAccountViewModel>("AccountDetails", model =>
        {
            model.Email = source.Email;
            model.Kinds = source.Kinds;
            model.Addresses = source.Addresses;
            model.Kind = source.Kind;
            model.DisplayName = source.DisplayName;
            model.FirstName = source.FirstName;
            model.LastName = source.LastName;
            model.CompanyName = source.CompanyName;
            model.TaxId = source.TaxId;
            model.Phone = source.Phone;
            model.AcceptsMarketing = source.AcceptsMarketing;
        });

    private ValueTask<IShape> AddressShapeAsync(CustomerAddressViewModel source)
        => _shapeFactory.CreateAsync<CustomerAddressViewModel>("AccountAddressEdit", model =>
        {
            model.CustomerId = source.CustomerId;
            model.CustomerName = source.CustomerName;
            model.AddressId = source.AddressId;
            model.IsNew = source.IsNew;
            model.IsOwn = source.IsOwn;
            model.AddressEditor = source.AddressEditor;
            model.Label = source.Label;
            model.Address = source.Address;
            model.IsDefaultBilling = source.IsDefaultBilling;
            model.IsDefaultShipping = source.IsDefaultShipping;
        });
}
