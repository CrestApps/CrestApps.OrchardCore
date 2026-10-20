using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Json;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// The Contact step of an order's checkout: who the order is for, and, as the site's setting asks, where it is billed.
/// A signed-in customer can bill one of their saved addresses.
/// </summary>
public sealed class CustomerContactCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    private const string BillingAddressPrefix = nameof(CustomerContactStepViewModel.BillingAddress);

    private readonly CustomerContactStepService _contactStep;
    private readonly CustomerAddressBook _addressBook;
    private readonly IAddressEditorService _addressEditor;
    private readonly IShapeFactory _shapeFactory;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    public CustomerContactCheckoutFlowDisplayDriver(
        CustomerContactStepService contactStep,
        CustomerAddressBook addressBook,
        IAddressEditorService addressEditor,
        IShapeFactory shapeFactory,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _contactStep = contactStep;
        _addressBook = addressBook;
        _addressEditor = addressEditor;
        _shapeFactory = shapeFactory;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    protected override string StepKey
        => CustomersConstants.ContactStepKey;

    protected override void BuildPrefix(CheckoutFlow model, string htmlFieldPrefix)
        => Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "Contact" : htmlFieldPrefix + ".Contact";

    protected override IDisplayResult EditStep(CheckoutFlow flow, BuildEditorContext context)
        => BuildEditor(flow, posted: null);

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        var model = new CustomerContactStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var saved = await _contactStep.SaveAsync(flow, new CustomerContactInput
        {
            Name = model.Name,
            Email = model.Email,
            Phone = model.Phone,
            BillingAddressId = model.BillingAddressId,
            BillingAddress = model.BillingAddress,
            SaveBillingAddress = model.SaveBillingAddress,
        }, context.Updater.ModelState, Prefix);

        return BuildEditor(flow, saved ? null : model);
    }

    private ShapeResult BuildEditor(CheckoutFlow flow, CustomerContactStepViewModel posted)
        => Initialize<CustomerContactStepViewModel>("CustomerContactStep_Edit", async model =>
        {
            var session = flow.Session;
            var customer = await GetCustomerAsync(session);
            var options = EditorOptions(customer);

            model.IsSignedIn = !string.IsNullOrEmpty(session.OwnerId);
            model.Requirement = CustomerContactCheckoutHandler.GetRequirement(session);

            if (posted is not null)
            {
                model.Name = posted.Name;
                model.Email = posted.Email;
                model.Phone = posted.Phone;
                model.BillingAddressId = posted.BillingAddressId;
                model.SaveBillingAddress = posted.SaveBillingAddress;
                model.BillingAddress = posted.BillingAddress ?? new AddressEditorViewModel();

                await _addressEditor.PopulateAsync(model.BillingAddress, options);
            }
            else if (GetStep(session) is { } step)
            {
                model.Name = step.Name;
                model.Email = step.Email;
                model.Phone = step.Phone;
                model.BillingAddressId = step.BillingAddressId;
                model.SaveBillingAddress = step.SaveBillingAddress;
                model.BillingAddress = await _addressEditor.BuildAsync(step.BillingAddressId is null ? step.BillingAddress : null, options);
            }
            else
            {
                // A signed-in customer starts with their own details and their default billing address; anyone else with
                // what the checkout already knows, such as the buyer an order was made for.
                var defaultAddress = customer?.GetDefaultBillingAddress();
                var known = session.TryGet<CheckoutContactInfo>(out var contact) ? contact : null;

                model.Name = (customer is null ? null : CustomerAddressBook.NewAddressFor(customer).Name ?? customer.GetName()) ?? known?.DisplayName;
                model.Email = customer?.Email ?? known?.Email;
                model.Phone = customer?.Phone ?? known?.Phone;
                model.BillingAddressId = defaultAddress?.AddressId;
                model.SaveBillingAddress = customer is not null;
                model.BillingAddress = await _addressEditor.BuildAsync(null, options);
            }

            model.SavedAddresses = customer is null ? [] : await _addressBook.SummarizeAsync(customer);

            // With saved addresses to pick from, "a new address" is a choice like the others.
            if (model.SavedAddresses.Count > 0)
            {
                model.BillingAddressId ??= string.Empty;
            }
            model.CanSaveAddress = customer is not null && (customer.Addresses?.Count ?? 0) < CustomerAddressBook.MaxAddresses;
            model.BillingAddressEditor = await _shapeFactory.CreateAddressEditorAsync(model.BillingAddress, Prefix + "." + BillingAddressPrefix);
        }).Location("Content");

    // The billing form asks for the address only: its recipient is the buyer named above, and its phone theirs.
    private static AddressEditorOptions EditorOptions(Customer customer)
        => CustomerContactStepService.BillingEditorOptions(customer);

    private Task<Customer> GetCustomerAsync(ICheckoutFlowSession session)
        => _contactStep.GetCustomerAsync(session);

    private CustomerContactStep GetStep(ICheckoutFlowSession session)
        => session.SavedSteps.TryGetPropertyValue(CustomersConstants.ContactStepKey, out var node) && node is not null
            ? node.Deserialize<CustomerContactStep>(_documentJsonSerializerOptions.SerializerOptions)
            : null;

}
