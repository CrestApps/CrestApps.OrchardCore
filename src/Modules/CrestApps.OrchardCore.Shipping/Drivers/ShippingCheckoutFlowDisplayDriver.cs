using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Json;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// The Shipping step: where the order goes, from the buyer's address book or entered, and how, from the rates the
/// address's zone offers. The rates are priced here, on the server; the buyer only picks one.
/// </summary>
/// <remarks>
/// The rates depend on the address, so an address posted without a rate chosen for it comes back with that address's
/// rates to choose from, as a step that isn't finished yet.
/// </remarks>
public sealed class ShippingCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    private const string AddressPrefix = nameof(ShippingCheckoutStepViewModel.Address);

    private readonly ShippingCheckoutStepService _step;
    private readonly ShippingOrderRateService _orderRates;
    private readonly IAddressEditorService _addressEditor;
    private readonly IAddressFormatter _addressFormatter;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    public ShippingCheckoutFlowDisplayDriver(
        ShippingCheckoutStepService step,
        ShippingOrderRateService orderRates,
        IAddressEditorService addressEditor,
        IAddressFormatter addressFormatter,
        IShapeFactory shapeFactory,
        ISiteService siteService,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<ShippingCheckoutFlowDisplayDriver> stringLocalizer)
    {
        _step = step;
        _orderRates = orderRates;
        _addressEditor = addressEditor;
        _addressFormatter = addressFormatter;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        S = stringLocalizer;
    }

    protected override string StepKey
        => ShippingConstants.CheckoutStepKey;

    protected override void BuildPrefix(CheckoutFlow model, string htmlFieldPrefix)
        => Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "Shipping" : htmlFieldPrefix + ".Shipping";

    protected override IDisplayResult EditStep(CheckoutFlow flow, BuildEditorContext context)
        => BuildEditor(flow, posted: null, quote: null);

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        var model = new ShippingCheckoutStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var result = await _step.SaveAsync(flow, new ShippingStepInput
        {
            PickupLocationId = model.PickupLocationId,
            AddressId = model.AddressId,
            Address = model.Address,
            RateId = model.RateId,
            SaveAddress = model.SaveAddress,
        }, context.Updater.ModelState, Prefix);

        model.IsChoosing = result.IsChoosing;

        return BuildEditor(flow, result.Saved ? null : model, result.Quote);
    }

    private ShapeResult BuildEditor(CheckoutFlow flow, ShippingCheckoutStepViewModel posted, ShippingQuote quote)
        => Initialize<ShippingCheckoutStepViewModel>("ShippingCheckoutStep_Edit", async model =>
        {
            var session = flow.Session;
            var customer = await GetCustomerAsync(session);
            var options = EditorOptions(session, customer);
            var step = ShippingCheckoutHandler.GetStep(session, _documentJsonSerializerOptions);
            Address known = null;

            model.PickupLocations = (await _siteService.GetSettingsAsync<ShippingSettings>()).PickupLocations ?? [];

            if (posted is not null)
            {
                model.PickupLocationId = posted.PickupLocationId;
                model.AddressId = posted.AddressId;
                model.RateId = posted.RateId;
                model.IsChoosing = posted.IsChoosing;
                model.SaveAddress = posted.SaveAddress;
                model.Address = posted.Address ?? new AddressEditorViewModel();

                await _addressEditor.PopulateAsync(model.Address, options);
            }
            else if (step is { PickupLocationId: null })
            {
                model.AddressId = step.AddressId;
                model.RateId = step.RateId;
                model.SaveAddress = step.SaveAddress;
                model.Address = await _addressEditor.BuildAsync(step.AddressId is null ? step.Address : null, options);
                known = step.Address;
            }
            else
            {
                // A customer starts with their default shipping address; anyone else with the billing address they
                // just gave, addressed to them. A buyer back on the step after choosing pickup sees the same, should
                // they change their mind.
                model.PickupLocationId = step?.PickupLocationId;

                var defaultAddress = customer?.GetDefaultShippingAddress();

                model.AddressId = defaultAddress?.AddressId;
                model.SaveAddress = customer is not null;
                model.Address = await _addressEditor.BuildAsync(defaultAddress is null ? GetBillingAddress(session) : null, options);
                known = defaultAddress?.ToAddress() ?? GetBillingAddress(session);
            }

            model.IsSignedIn = !string.IsNullOrEmpty(session.OwnerId);
            model.SavedAddresses = await SummarizeAsync(customer);

            if (model.SavedAddresses.Count > 0)
            {
                model.AddressId ??= string.Empty;
            }

            model.CanSaveAddress = customer is not null && (customer.Addresses?.Count ?? 0) < 20;
            model.AddressEditor = await _shapeFactory.CreateAddressEditorAsync(model.Address, Prefix + "." + AddressPrefix);

            // Rates for the address the buyer is most likely shipping to, so they can choose straight away.
            if (quote is null && IsComplete(known) && await GetOrderAsync(session) is { } order)
            {
                quote = await _orderRates.QuoteAsync(order, known);
            }

            model.Currency = session.Currency;
            model.Rates = quote?.Rates ?? [];
            model.HasZone = quote?.Zone is not null;

            if (model.Rates.Count > 0 && !model.Rates.Any(rate => rate.RateId == model.RateId))
            {
                model.RateId = model.Rates[0].RateId;
            }
        }).Location("Content");

    private async Task<IReadOnlyList<ShippingAddressSummary>> SummarizeAsync(Customer customer)
    {
        if (customer?.Addresses is not { Count: > 0 } addresses)
        {
            return [];
        }

        var summaries = new List<ShippingAddressSummary>();

        // The default shipping address first.
        foreach (var entry in addresses.OrderByDescending(entry => entry.AddressId == customer.DefaultShippingAddressId))
        {
            summaries.Add(new ShippingAddressSummary
            {
                AddressId = entry.AddressId,
                Label = entry.Label,
                Lines = await _addressFormatter.GetLinesAsync(entry.ToAddress(), new AddressFormatOptions { IncludeCountry = true }),
                IsDefault = entry.AddressId == customer.DefaultShippingAddressId,
            });
        }

        return summaries;
    }

    // The shipping form asks for who receives the parcel and a phone the carrier can call.
    private static AddressEditorOptions EditorOptions(ICheckoutFlowSession session, Customer customer)
        => ShippingCheckoutStepService.EditorOptions(session, customer);

    private static Address GetBillingAddress(ICheckoutFlowSession session)
        => session.TryGet<CheckoutContactInfo>(out var contact) ? contact.BillingAddress : null;

    // Enough to find the zone and price shipping: a country, and a first line to ship to.
    private static bool IsComplete(Address address)
        => !string.IsNullOrWhiteSpace(address?.Country) && !string.IsNullOrWhiteSpace(address.AddressLine1);

    private Task<Order> GetOrderAsync(ICheckoutFlowSession session)
        => _step.GetOrderAsync(session);

    private Task<Customer> GetCustomerAsync(ICheckoutFlowSession session)
        => _step.GetCustomerAsync(session);
}
