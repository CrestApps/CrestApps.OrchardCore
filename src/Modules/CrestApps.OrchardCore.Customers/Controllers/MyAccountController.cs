using System.Security.Claims;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using YesSql;

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
    private readonly ICustomerService _customerService;
    private readonly ICustomerStore _store;
    private readonly CustomerAddressBook _addressBook;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public MyAccountController(
        ICustomerService customerService,
        ICustomerStore store,
        CustomerAddressBook addressBook,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IStringLocalizer<MyAccountController> stringLocalizer,
        IHtmlLocalizer<MyAccountController> htmlLocalizer)
    {
        _customerService = customerService;
        _store = store;
        _addressBook = addressBook;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        S = stringLocalizer;
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

        return View(await BuildAccountAsync(customer, input: null));
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

        if (model.Kind == CustomerKind.Business && string.IsNullOrWhiteSpace(model.CompanyName))
        {
            ModelState.AddModelError(nameof(model.CompanyName), S["A business needs a company name."]);
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildAccountAsync(customer, model));
        }

        customer.Kind = model.Kind;
        customer.DisplayName = Trim(model.DisplayName);
        customer.FirstName = Trim(model.FirstName);
        customer.LastName = Trim(model.LastName);
        customer.CompanyName = Trim(model.CompanyName);
        customer.TaxId = Trim(model.TaxId);
        customer.Phone = Trim(model.Phone);
        customer.AcceptsMarketing = model.AcceptsMarketing;

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

    // The signed-in user's customer, made the first time they open the page. Null when they may not manage it.
    private async Task<Customer> GetCustomerAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || !await _authorizationService.AuthorizeAsync(User, CustomerPermissions.ManageOwnCustomerProfile))
        {
            return null;
        }

        var customer = await _customerService.GetOrCreateForUserAsync(userId, HttpContext.RequestAborted);

        // A disabled or erased customer is not offered an account page.
        return customer?.Status == CustomerStatus.Active ? customer : null;
    }

    private async Task<MyAccountViewModel> BuildAccountAsync(Customer customer, MyAccountViewModel input)
    {
        var model = input ?? new MyAccountViewModel
        {
            Kind = customer.Kind,
            DisplayName = customer.DisplayName,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            CompanyName = customer.CompanyName,
            TaxId = customer.TaxId,
            Phone = customer.Phone,
            AcceptsMarketing = customer.AcceptsMarketing,
        };

        model.Email = customer.Email;
        model.Addresses = await _addressBook.SummarizeAsync(customer);
        model.Kinds =
        [
            new SelectListItem(S["Myself"], nameof(CustomerKind.Person)),
            new SelectListItem(S["A business"], nameof(CustomerKind.Business)),
        ];

        return model;
    }

    private async Task<bool> TrySaveAsync(Customer customer)
    {
        var now = _clock.UtcNow;

        customer.ModifiedUtc = now;
        customer.LastActivityUtc = now;

        try
        {
            await _store.UpdateAsync(customer, HttpContext.RequestAborted);

            return true;
        }
        catch (ConcurrencyException)
        {
            await _notifier.ErrorAsync(H["Your details were changed somewhere else at the same time. Your change was not saved; try again."]);

            return false;
        }
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
