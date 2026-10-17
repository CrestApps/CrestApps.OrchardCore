using CrestApps.Core;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// What the administrators' and the customers' own address pages share: the address editor, the checks and saving an
/// address to the book.
/// </summary>
public sealed class CustomerAddressBook
{
    /// <summary>
    /// Enough for anyone's homes, offices and family; it keeps a customer's record small.
    /// </summary>
    public const int MaxAddresses = 20;

    /// <summary>
    /// The prefix the address editor's fields post under.
    /// </summary>
    public const string EditorPrefix = nameof(CustomerAddressViewModel.Address);

    private readonly IAddressEditorService _addressEditor;
    private readonly IAddressFormatter _addressFormatter;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IStringLocalizer S;

    public CustomerAddressBook(
        IAddressEditorService addressEditor,
        IAddressFormatter addressFormatter,
        IShapeFactory shapeFactory,
        IStringLocalizer<CustomerAddressBook> stringLocalizer)
    {
        _addressEditor = addressEditor;
        _addressFormatter = addressFormatter;
        _shapeFactory = shapeFactory;
        S = stringLocalizer;
    }

    /// <summary>
    /// An address book entry is for mail and deliveries: it names who receives it, and keeps a phone for the courier.
    /// A new one starts in the country of the customer's other addresses.
    /// </summary>
    public static AddressEditorOptions EditorOptionsFor(Customer customer)
        => new()
        {
            RequireName = true,
            ShowPhone = true,
            DefaultCountry = customer?.GetDefaultBillingAddress()?.Address?.Country
                ?? customer?.GetDefaultShippingAddress()?.Address?.Country
                ?? customer?.Addresses?.FirstOrDefault()?.Address?.Country,
        };

    public async Task<IReadOnlyList<CustomerAddressSummary>> SummarizeAsync(Customer customer)
    {
        var summaries = new List<CustomerAddressSummary>();

        foreach (var address in customer.Addresses ?? [])
        {
            summaries.Add(new CustomerAddressSummary
            {
                AddressId = address.AddressId,
                Label = address.Label,
                Lines = await _addressFormatter.GetLinesAsync(address.ToAddress()),
                IsDefaultBilling = string.Equals(customer.DefaultBillingAddressId, address.AddressId, StringComparison.Ordinal),
                IsDefaultShipping = string.Equals(customer.DefaultShippingAddressId, address.AddressId, StringComparison.Ordinal),
            });
        }

        return summaries;
    }

    public async Task<CustomerAddressViewModel> BuildEditorAsync(Customer customer, CustomerAddress address, CustomerAddressViewModel input = null)
    {
        var options = EditorOptionsFor(customer);
        var model = input ?? new CustomerAddressViewModel();

        if (input is not null)
        {
            model.Address ??= new AddressEditorViewModel();

            await _addressEditor.PopulateAsync(model.Address, options);
        }
        else if (address is not null)
        {
            model.Label = address.Label;
            model.Address = await _addressEditor.BuildAsync(address.Address, options);
            model.IsDefaultBilling = string.Equals(customer.DefaultBillingAddressId, address.AddressId, StringComparison.Ordinal);
            model.IsDefaultShipping = string.Equals(customer.DefaultShippingAddressId, address.AddressId, StringComparison.Ordinal);
        }
        else
        {
            // A first address becomes the customer's default.
            model.Address = await _addressEditor.BuildAsync(NewAddressFor(customer), options);
            model.IsDefaultBilling = customer.GetDefaultBillingAddress() is null;
            model.IsDefaultShipping = customer.GetDefaultShippingAddress() is null;
        }

        model.CustomerId = customer.ItemId;
        model.CustomerName = customer.GetName();
        model.AddressId = address?.AddressId;
        model.IsNew = address is null;
        model.AddressEditor = await _shapeFactory.CreateAddressEditorAsync(model.Address, EditorPrefix);

        return model;
    }

    /// <summary>
    /// A new address starts with the customer's own details. The recipient is a person; a business's name goes in
    /// Company rather than being repeated as the recipient.
    /// </summary>
    public static Address NewAddressFor(Customer customer)
    {
        var personName = string.Join(' ', new[] { customer.FirstName, customer.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new Address
        {
            Name = !string.IsNullOrEmpty(personName)
                ? personName
                : customer.Kind == CustomerKind.Business ? null : customer.GetName(),
            Company = customer.Kind == CustomerKind.Business ? customer.CompanyName : null,
            Phone = customer.Phone,
        };
    }

    /// <summary>
    /// Checks the posted address against its country and the book's limit. Returns the address to keep, tidied, or
    /// <see langword="null"/> when something is wrong; what is wrong is in <paramref name="modelState"/>.
    /// </summary>
    public async Task<Address> ValidateAsync(Customer customer, CustomerAddressViewModel model, bool isNew, ModelStateDictionary modelState)
    {
        var result = await _addressEditor.ValidateAsync(model.Address ?? new AddressEditorViewModel(), modelState, EditorPrefix, EditorOptionsFor(customer));

        if (isNew && (customer.Addresses?.Count ?? 0) >= MaxAddresses)
        {
            modelState.AddModelError(string.Empty, S["An address book holds up to {0} addresses. Delete one to add another.", MaxAddresses]);
        }

        return result.IsValid && modelState.IsValid ? result.Address : null;
    }

    /// <summary>
    /// Saves the address to the customer's book: a new one is added, an existing one changed in place.
    /// </summary>
    public static CustomerAddress Apply(Customer customer, CustomerAddress entry, CustomerAddressViewModel model, Address address)
    {
        if (entry is null)
        {
            entry = new CustomerAddress { AddressId = UniqueId.GenerateId() };
            customer.Addresses ??= [];
            customer.Addresses.Add(entry);
        }

        entry.Label = string.IsNullOrWhiteSpace(model.Label) ? null : model.Label.Trim();
        entry.Address = address;

        if (model.IsDefaultBilling)
        {
            customer.DefaultBillingAddressId = entry.AddressId;
        }
        else if (string.Equals(customer.DefaultBillingAddressId, entry.AddressId, StringComparison.Ordinal))
        {
            customer.DefaultBillingAddressId = null;
        }

        if (model.IsDefaultShipping)
        {
            customer.DefaultShippingAddressId = entry.AddressId;
        }
        else if (string.Equals(customer.DefaultShippingAddressId, entry.AddressId, StringComparison.Ordinal))
        {
            customer.DefaultShippingAddressId = null;
        }

        return entry;
    }
}
