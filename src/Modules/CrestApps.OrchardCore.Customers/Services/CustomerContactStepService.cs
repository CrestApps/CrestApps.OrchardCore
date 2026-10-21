using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Customers.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// The checkout's Contact step: who the order is for, how to reach them, and where it's billed. The step's page and
/// the commerce API both save it through here.
/// </summary>
public sealed class CustomerContactStepService
{
    private static readonly EmailAddressAttribute _emailAddress = new();

    private readonly ICustomerService _customerService;
    private readonly IAddressEditorService _addressEditor;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;

    internal readonly IStringLocalizer S;

    public CustomerContactStepService(
        ICustomerService customerService,
        IAddressEditorService addressEditor,
        CheckoutInvoiceBuilder invoiceBuilder,
        IStringLocalizer<CustomerContactStepService> stringLocalizer)
    {
        _customerService = customerService;
        _addressEditor = addressEditor;
        _invoiceBuilder = invoiceBuilder;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the signed-in buyer's customer, made on their first checkout, or <see langword="null"/> for a guest.
    /// </summary>
    public async Task<Customer> GetCustomerAsync(ICheckoutFlowSession session)
        => string.IsNullOrEmpty(session?.OwnerId) ? null : await _customerService.GetOrCreateForUserAsync(session.OwnerId);

    /// <summary>
    /// Returns the options the billing address is entered with: the address only, since its recipient is the buyer.
    /// </summary>
    public static AddressEditorOptions BillingEditorOptions(Customer customer)
        => new()
        {
            ShowName = false,
            ShowCompany = true,
            ShowPhone = false,
            DefaultCountry = CustomerAddressBook.EditorOptionsFor(customer).DefaultCountry,
        };

    /// <summary>
    /// Saves the step when what was entered is valid, and rebuilds the invoice, which an order that doesn't ship is
    /// taxed by. Problems are added to <paramref name="modelState"/>, keyed under <paramref name="prefix"/>.
    /// </summary>
    public async Task<bool> SaveAsync(CheckoutFlow flow, CustomerContactInput input, ModelStateDictionary modelState, string prefix)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(modelState);

        var session = flow.Session;
        var requirement = CustomerContactCheckoutHandler.GetRequirement(session);
        var customer = await GetCustomerAsync(session);
        var name = Clean(input.Name);
        var email = Clean(input.Email);
        var phone = Clean(input.Phone);

        if (name is null)
        {
            modelState.AddModelError(Key(prefix, nameof(input.Name)), S["Enter your name."]);
        }

        if (email is null)
        {
            modelState.AddModelError(Key(prefix, nameof(input.Email)), S["Enter your email, so we can send you the order's details."]);
        }
        else if (!_emailAddress.IsValid(email))
        {
            modelState.AddModelError(Key(prefix, nameof(input.Email)), S["Enter a valid email address."]);
        }

        Address billingAddress = null;
        string billingAddressId = null;

        if (requirement != BillingAddressRequirement.Off)
        {
            var saved = string.IsNullOrEmpty(input.BillingAddressId) ? null : customer?.FindAddress(input.BillingAddressId);

            if (saved is not null)
            {
                billingAddress = saved.ToAddress();
                billingAddressId = saved.AddressId;
            }
            else if (requirement == BillingAddressRequirement.Required || input.BillingAddress?.HasAnyValue() == true)
            {
                var result = await _addressEditor.ValidateAsync(input.BillingAddress ?? new AddressEditorViewModel(), modelState, Key(prefix, nameof(input.BillingAddress)), BillingEditorOptions(customer));

                if (result.IsValid)
                {
                    // The billing address is addressed to the buyer, and reaches them by their phone.
                    billingAddress = new Address
                    {
                        Name = name,
                        Company = result.Address.Company,
                        AddressLine1 = result.Address.AddressLine1,
                        AddressLine2 = result.Address.AddressLine2,
                        DependentLocality = result.Address.DependentLocality,
                        City = result.Address.City,
                        Region = result.Address.Region,
                        PostalCode = result.Address.PostalCode,
                        SortingCode = result.Address.SortingCode,
                        Country = result.Address.Country,
                        Phone = phone,
                    };
                }
            }
        }

        if (!modelState.IsValid)
        {
            return false;
        }

        var step = new CustomerContactStep
        {
            Name = name,
            Email = email,
            Phone = phone,
            BillingAddressId = billingAddressId,
            BillingAddress = billingAddress,
            SaveBillingAddress = billingAddressId is null && billingAddress is not null && customer is not null && input.SaveBillingAddress,
        };

        session.SavedSteps[CustomersConstants.ContactStepKey] = JObject.FromObject(step);

        // Who to reach about the order: the order, its receipts and a Pay Later reminder all read it from here.
        session.Put(new CheckoutContactInfo
        {
            DisplayName = name,
            Email = email,
            Phone = phone,
            BillingAddress = billingAddress,
        });

        await _invoiceBuilder.BuildAsync(flow);

        return true;
    }

    private static string Key(string prefix, string field)
        => string.IsNullOrEmpty(prefix) ? field : prefix + "." + field;

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// What the buyer enters on the Contact step.
/// </summary>
public sealed class CustomerContactInput
{
    public string Name { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets a saved address of the signed-in buyer's to bill, or <see langword="null"/> for a new one.
    /// </summary>
    public string BillingAddressId { get; set; }

    public AddressEditorViewModel BillingAddress { get; set; }

    public bool SaveBillingAddress { get; set; }
}
