using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;

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
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IHtmlLocalizer H;

    public AccountProfileController(
        CustomerAccountPages pages,
        CustomerAddressBook addressBook,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IHtmlLocalizer<AccountProfileController> htmlLocalizer)
    {
        _pages = pages;
        _addressBook = addressBook;
        _notifier = notifier;
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

        _pages.Validate(model, ModelState);

        if (!ModelState.IsValid)
        {
            return View(await DetailsShapeAsync(await _pages.BuildAccountAsync(customer, model)));
        }

        CustomerAccountPages.Apply(customer, model);

        if (await SaveAsync(customer))
        {
            await _notifier.SuccessAsync(H["Your details were saved."]);
        }

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

        var model = await _addressBook.BuildEditorAsync(customer, null);
        model.IsOwn = true;

        return View("AddressEdit", await AddressShapeAsync(model));
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

        var model = await _addressBook.BuildEditorAsync(customer, address);
        model.IsOwn = true;

        return View(await AddressShapeAsync(model));
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

        if (!customer.RemoveAddress(addressId))
        {
            return NotFound();
        }

        if (await SaveAsync(customer))
        {
            await _notifier.SuccessAsync(H["The address was deleted."]);
        }

        return RedirectToAction(nameof(Addresses));
    }

    private async Task<IActionResult> SaveAddressAsync(string addressId, CustomerAddressViewModel model)
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
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

            return View("AddressEdit", await AddressShapeAsync(invalid));
        }

        CustomerAddressBook.Apply(customer, address, model, value);

        if (await SaveAsync(customer))
        {
            await _notifier.SuccessAsync(H["The address was saved."]);
        }

        return RedirectToAction(nameof(Addresses));
    }

    private async Task<bool> SaveAsync(Customer customer)
    {
        if (await _pages.TrySaveAsync(customer, HttpContext.RequestAborted))
        {
            return true;
        }

        await _notifier.ErrorAsync(H["Your details were changed somewhere else at the same time. Your change was not saved; try again."]);

        return false;
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
