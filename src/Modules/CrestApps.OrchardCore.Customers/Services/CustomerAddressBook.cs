using CrestApps.Core;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// What the administrators' and the customers' own address pages share: the country list, the checks and saving an
/// address to the book.
/// </summary>
public sealed class CustomerAddressBook
{
    /// <summary>
    /// Enough for anyone's homes, offices and family; it keeps a customer's record small.
    /// </summary>
    public const int MaxAddresses = 20;

    private readonly ICountryService _countryService;

    internal readonly IStringLocalizer S;

    public CustomerAddressBook(ICountryService countryService, IStringLocalizer<CustomerAddressBook> stringLocalizer)
    {
        _countryService = countryService;
        S = stringLocalizer;
    }

    public async Task<Func<string, string>> GetCountryNamesAsync()
    {
        var countries = (await _countryService.GetCountriesAsync())
            .GroupBy(country => country.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

        return code => code is not null && countries.TryGetValue(code, out var name) ? name : code;
    }

    public async Task<IReadOnlyList<CustomerAddressSummary>> SummarizeAsync(Customer customer)
    {
        var countryName = await GetCountryNamesAsync();

        return (customer.Addresses ?? [])
            .Select(address => new CustomerAddressSummary
            {
                AddressId = address.AddressId,
                Label = address.Label,
                Lines = address.GetLines(countryName),
                IsDefaultBilling = string.Equals(customer.DefaultBillingAddressId, address.AddressId, StringComparison.Ordinal),
                IsDefaultShipping = string.Equals(customer.DefaultShippingAddressId, address.AddressId, StringComparison.Ordinal),
            })
            .ToArray();
    }

    public async Task<CustomerAddressViewModel> BuildEditorAsync(Customer customer, CustomerAddress address, CustomerAddressViewModel input = null)
    {
        var model = input ?? new CustomerAddressViewModel();

        if (input is null && address is not null)
        {
            model.Label = address.Label;
            model.Name = address.Name;
            model.Company = address.Company;
            model.AddressLine1 = address.AddressLine1;
            model.AddressLine2 = address.AddressLine2;
            model.City = address.City;
            model.Region = address.Region;
            model.PostalCode = address.PostalCode;
            model.Country = address.Country;
            model.Phone = address.Phone;
            model.IsDefaultBilling = string.Equals(customer.DefaultBillingAddressId, address.AddressId, StringComparison.Ordinal);
            model.IsDefaultShipping = string.Equals(customer.DefaultShippingAddressId, address.AddressId, StringComparison.Ordinal);
        }
        else if (input is null)
        {
            // A new address starts with the customer's own details, and a first one becomes their default. The
            // recipient is a person; a business's name goes in Company.
            var personName = string.Join(' ', new[] { customer.FirstName, customer.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

            model.Name = !string.IsNullOrEmpty(personName)
                ? personName
                : customer.Kind == CustomerKind.Business ? null : customer.GetName();
            model.Company = customer.Kind == CustomerKind.Business ? customer.CompanyName : null;
            model.Phone = customer.Phone;
            model.IsDefaultBilling = customer.GetDefaultBillingAddress() is null;
            model.IsDefaultShipping = customer.GetDefaultShippingAddress() is null;
        }

        model.CustomerId = customer.ItemId;
        model.CustomerName = customer.GetName();
        model.AddressId = address?.AddressId;
        model.IsNew = address is null;
        model.Countries = (await _countryService.GetCountriesAsync())
            .Select(country => new SelectListItem(country.Name, country.Code, string.Equals(country.Code, model.Country, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return model;
    }

    public async Task ValidateAsync(Customer customer, CustomerAddressViewModel model, bool isNew, ModelStateDictionary modelState)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            modelState.AddModelError(nameof(model.Name), S["Enter who receives mail at this address."]);
        }

        if (string.IsNullOrWhiteSpace(model.AddressLine1))
        {
            modelState.AddModelError(nameof(model.AddressLine1), S["Enter the street address."]);
        }

        if (string.IsNullOrWhiteSpace(model.City))
        {
            modelState.AddModelError(nameof(model.City), S["Enter the city."]);
        }

        var countries = await _countryService.GetCountriesAsync();

        if (string.IsNullOrWhiteSpace(model.Country) || !countries.Any(country => string.Equals(country.Code, model.Country, StringComparison.OrdinalIgnoreCase)))
        {
            modelState.AddModelError(nameof(model.Country), S["Choose a country."]);
        }

        if (isNew && (customer.Addresses?.Count ?? 0) >= MaxAddresses)
        {
            modelState.AddModelError(string.Empty, S["An address book holds up to {0} addresses. Delete one to add another.", MaxAddresses]);
        }
    }

    /// <summary>
    /// Saves the address to the customer's book: a new one is added, an existing one changed in place.
    /// </summary>
    public static CustomerAddress Apply(Customer customer, CustomerAddress address, CustomerAddressViewModel model)
    {
        if (address is null)
        {
            address = new CustomerAddress { AddressId = UniqueId.GenerateId() };
            customer.Addresses ??= [];
            customer.Addresses.Add(address);
        }

        address.Label = Trim(model.Label);
        address.Name = Trim(model.Name);
        address.Company = Trim(model.Company);
        address.AddressLine1 = Trim(model.AddressLine1);
        address.AddressLine2 = Trim(model.AddressLine2);
        address.City = Trim(model.City);
        address.Region = Trim(model.Region);
        address.PostalCode = Trim(model.PostalCode);
        address.Country = Trim(model.Country)?.ToUpperInvariant();
        address.Phone = Trim(model.Phone);

        if (model.IsDefaultBilling)
        {
            customer.DefaultBillingAddressId = address.AddressId;
        }
        else if (string.Equals(customer.DefaultBillingAddressId, address.AddressId, StringComparison.Ordinal))
        {
            customer.DefaultBillingAddressId = null;
        }

        if (model.IsDefaultShipping)
        {
            customer.DefaultShippingAddressId = address.AddressId;
        }
        else if (string.Equals(customer.DefaultShippingAddressId, address.AddressId, StringComparison.Ordinal))
        {
            customer.DefaultShippingAddressId = null;
        }

        return address;
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
