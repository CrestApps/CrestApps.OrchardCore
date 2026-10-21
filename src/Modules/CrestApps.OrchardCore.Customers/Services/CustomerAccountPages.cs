using System.Security.Claims;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// What a signed-in customer's own profile pages share, in the admin's My Account, on the storefront account and over
/// the API: finding their customer, the profile form, and saving it and their address book.
/// </summary>
public sealed class CustomerAccountPages
{
    private readonly ICustomerService _customerService;
    private readonly ICustomerStore _store;
    private readonly CustomerAddressBook _addressBook;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public CustomerAccountPages(
        ICustomerService customerService,
        ICustomerStore store,
        CustomerAddressBook addressBook,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IStringLocalizer<CustomerAccountPages> stringLocalizer,
        IHtmlLocalizer<CustomerAccountPages> htmlLocalizer)
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

    /// <summary>
    /// Returns the signed-in user's customer, made the first time they open a page, or <see langword="null"/> when they
    /// may not manage it, or it is disabled or erased.
    /// </summary>
    public async Task<Customer> GetCustomerAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || !await _authorizationService.AuthorizeAsync(user, CustomerPermissions.ManageOwnCustomerProfile))
        {
            return null;
        }

        var customer = await _customerService.GetOrCreateForUserAsync(userId, cancellationToken);

        return customer?.Status == CustomerStatus.Active ? customer : null;
    }

    public async Task<MyAccountViewModel> BuildAccountAsync(Customer customer, MyAccountViewModel input)
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

    private void Validate(MyAccountViewModel model, ModelStateDictionary modelState)
    {
        if (model.Kind == CustomerKind.Business && string.IsNullOrWhiteSpace(model.CompanyName))
        {
            modelState.AddModelError(nameof(model.CompanyName), S["A business needs a company name."]);
        }
    }

    private static void Apply(Customer customer, MyAccountViewModel model)
    {
        customer.Kind = model.Kind;
        customer.DisplayName = Trim(model.DisplayName);
        customer.FirstName = Trim(model.FirstName);
        customer.LastName = Trim(model.LastName);
        customer.CompanyName = Trim(model.CompanyName);
        customer.TaxId = Trim(model.TaxId);
        customer.Phone = Trim(model.Phone);
        customer.AcceptsMarketing = model.AcceptsMarketing;
    }

    /// <summary>
    /// Checks the profile form and saves it to the customer.
    /// </summary>
    public async Task<CustomerAccountSaveResult> SaveProfileAsync(Customer customer, MyAccountViewModel model, ModelStateDictionary modelState, CancellationToken cancellationToken = default)
    {
        Validate(model, modelState);

        if (!modelState.IsValid)
        {
            return CustomerAccountSaveResult.Invalid;
        }

        Apply(customer, model);

        return await TrySaveAsync(customer, cancellationToken)
            ? CustomerAccountSaveResult.Saved
            : CustomerAccountSaveResult.Conflict;
    }

    /// <summary>
    /// The editor of one of the customer's own addresses, or of a new one when <paramref name="address"/> is
    /// <see langword="null"/>. <paramref name="input"/> is the posted form, shown again when it has errors.
    /// </summary>
    public async Task<CustomerAddressViewModel> BuildAddressEditorAsync(Customer customer, CustomerAddress address, CustomerAddressViewModel input = null)
    {
        var model = await _addressBook.BuildEditorAsync(customer, address, input);
        model.IsOwn = true;

        return model;
    }

    /// <summary>
    /// Checks the posted address and saves it to the customer's address book: a new one when
    /// <paramref name="addressId"/> is <see langword="null"/>, otherwise the address with that id.
    /// </summary>
    /// <returns>How it went, and the address book entry that was saved.</returns>
    public async Task<(CustomerAccountSaveResult Result, CustomerAddress Entry)> SaveAddressAsync(
        Customer customer,
        string addressId,
        CustomerAddressViewModel model,
        ModelStateDictionary modelState,
        CancellationToken cancellationToken = default)
    {
        var entry = customer.FindAddress(addressId);

        if (addressId is not null && entry is null)
        {
            return (CustomerAccountSaveResult.NotFound, null);
        }

        if (await _addressBook.ValidateAsync(customer, model, entry is null, modelState) is not { } address)
        {
            return (CustomerAccountSaveResult.Invalid, entry);
        }

        entry = CustomerAddressBook.Apply(customer, entry, model, address);

        return await TrySaveAsync(customer, cancellationToken)
            ? (CustomerAccountSaveResult.Saved, entry)
            : (CustomerAccountSaveResult.Conflict, entry);
    }

    /// <summary>
    /// Deletes an address from the customer's address book.
    /// </summary>
    public async Task<CustomerAccountSaveResult> DeleteAddressAsync(Customer customer, string addressId, CancellationToken cancellationToken = default)
    {
        if (!customer.RemoveAddress(addressId))
        {
            return CustomerAccountSaveResult.NotFound;
        }

        return await TrySaveAsync(customer, cancellationToken)
            ? CustomerAccountSaveResult.Saved
            : CustomerAccountSaveResult.Conflict;
    }

    /// <summary>
    /// Tells the customer on the next page that their change was saved, with <paramref name="saved"/>, or that it
    /// was not because the customer was changed somewhere else at the same time.
    /// </summary>
    public async Task NotifyAsync(CustomerAccountSaveResult result, LocalizedHtmlString saved)
    {
        if (result == CustomerAccountSaveResult.Saved)
        {
            await _notifier.SuccessAsync(saved);
        }
        else if (result == CustomerAccountSaveResult.Conflict)
        {
            await _notifier.ErrorAsync(H["Your details were changed somewhere else at the same time. Your change was not saved; try again."]);
        }
    }

    /// <summary>
    /// Saves the customer, or returns <see langword="false"/> when it was changed elsewhere at the same time.
    /// </summary>
    private async Task<bool> TrySaveAsync(Customer customer, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        customer.ModifiedUtc = now;
        customer.LastActivityUtc = now;

        try
        {
            await _store.UpdateAsync(customer, cancellationToken);

            return true;
        }
        catch (ConcurrencyException)
        {
            return false;
        }
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
