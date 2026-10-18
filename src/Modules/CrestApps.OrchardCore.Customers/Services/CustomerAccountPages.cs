using System.Security.Claims;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// What a signed-in customer's own profile pages share, in the admin's My Account and on the storefront account:
/// finding their customer, the profile form, and saving it.
/// </summary>
public sealed class CustomerAccountPages
{
    private readonly ICustomerService _customerService;
    private readonly ICustomerStore _store;
    private readonly CustomerAddressBook _addressBook;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public CustomerAccountPages(
        ICustomerService customerService,
        ICustomerStore store,
        CustomerAddressBook addressBook,
        IAuthorizationService authorizationService,
        IClock clock,
        IStringLocalizer<CustomerAccountPages> stringLocalizer)
    {
        _customerService = customerService;
        _store = store;
        _addressBook = addressBook;
        _authorizationService = authorizationService;
        _clock = clock;
        S = stringLocalizer;
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

    public void Validate(MyAccountViewModel model, ModelStateDictionary modelState)
    {
        if (model.Kind == CustomerKind.Business && string.IsNullOrWhiteSpace(model.CompanyName))
        {
            modelState.AddModelError(nameof(model.CompanyName), S["A business needs a company name."]);
        }
    }

    public static void Apply(Customer customer, MyAccountViewModel model)
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
    /// Saves the customer, or returns <see langword="false"/> when it was changed elsewhere at the same time.
    /// </summary>
    public async Task<bool> TrySaveAsync(Customer customer, CancellationToken cancellationToken = default)
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
