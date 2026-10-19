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
    private const string ShippingLineId = "shipping";

    private readonly IOrderStore _orderStore;
    private readonly ShippingOrderRateService _orderRates;
    private readonly IAddressEditorService _addressEditor;
    private readonly IAddressFormatter _addressFormatter;
    private readonly IShapeFactory _shapeFactory;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISiteService _siteService;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    public ShippingCheckoutFlowDisplayDriver(
        IOrderStore orderStore,
        ShippingOrderRateService orderRates,
        IAddressEditorService addressEditor,
        IAddressFormatter addressFormatter,
        IShapeFactory shapeFactory,
        CheckoutInvoiceBuilder invoiceBuilder,
        IServiceProvider serviceProvider,
        ISiteService siteService,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<ShippingCheckoutFlowDisplayDriver> stringLocalizer)
    {
        _orderStore = orderStore;
        _orderRates = orderRates;
        _addressEditor = addressEditor;
        _addressFormatter = addressFormatter;
        _shapeFactory = shapeFactory;
        _invoiceBuilder = invoiceBuilder;
        _serviceProvider = serviceProvider;
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

        var modelState = context.Updater.ModelState;
        var session = flow.Session;
        var order = await GetOrderAsync(session);

        if (order is null)
        {
            modelState.AddModelError(Prefix, nameof(model.RateId), S["This order can no longer be found. Go back to your cart and check out again."]);

            return BuildEditor(flow, model, quote: null);
        }

        // Picked up from one of the store's locations: no address to enter and nothing to quote, the location is where
        // the order goes and what it's taxed by.
        if (!string.IsNullOrEmpty(model.PickupLocationId))
        {
            var location = (await _siteService.GetSettingsAsync<ShippingSettings>()).PickupLocations?
                .FirstOrDefault(entry => string.Equals(entry.LocationId, model.PickupLocationId, StringComparison.Ordinal));

            if (location is null)
            {
                modelState.AddModelError(Prefix, nameof(model.PickupLocationId), S["That pickup location is no longer offered. Choose another, or have the order shipped."]);

                return BuildEditor(flow, model, quote: null);
            }

            var pickupAddress = location.ToAddress();

            Choose(session, new ShippingCheckoutStep
            {
                PickupLocationId = location.LocationId,
                Address = pickupAddress,
                RateId = PickupRateId(location),
            }, pickupAddress, CreatePickupRate(location), location.StockLocationId);

            await _invoiceBuilder.BuildAsync(flow);

            return BuildEditor(flow, posted: null, quote: null);
        }

        var customer = await GetCustomerAsync(session);
        var saved = string.IsNullOrEmpty(model.AddressId) ? null : customer?.FindAddress(model.AddressId);
        Address address = null;

        if (saved is not null)
        {
            address = saved.ToAddress();
        }
        else
        {
            var result = await _addressEditor.ValidateAsync(model.Address ?? new AddressEditorViewModel(), modelState, Prefix + "." + AddressPrefix, EditorOptions(session, customer));

            if (result.IsValid)
            {
                address = result.Address;
            }
        }

        if (address is null || !modelState.IsValid)
        {
            return BuildEditor(flow, model, quote: null);
        }

        var quote = await _orderRates.QuoteAsync(order, address);

        if (quote.Zone is null)
        {
            modelState.AddModelError(Prefix, nameof(model.RateId), S["We don't ship to this address. Choose another address."]);

            return BuildEditor(flow, model, quote);
        }

        if (quote.Rates.Count == 0)
        {
            modelState.AddModelError(Prefix, nameof(model.RateId), S["There is no way to ship this order to this address. Choose another address, or contact us."]);

            return BuildEditor(flow, model, quote);
        }

        // With one way to ship, there is nothing to choose; with several, the buyer picks one for this address.
        var rate = quote.Rates.Count == 1
            ? quote.Rates[0]
            : quote.Rates.FirstOrDefault(rate => string.Equals(rate.RateId, model.RateId, StringComparison.Ordinal));

        if (rate is null)
        {
            if (string.IsNullOrEmpty(model.RateId))
            {
                // The ways to ship to this address are shown for the first time. The buyer stays on the step to look
                // at them and their prices, which isn't a mistake, so the error that keeps the step has no message.
                modelState.AddModelError(Prefix + ".Rates", string.Empty);
                model.IsChoosing = true;
            }
            else
            {
                modelState.AddModelError(Prefix, nameof(model.RateId), S["That way to ship isn't offered for this address. Choose another."]);
            }

            return BuildEditor(flow, model, quote);
        }

        Choose(session, new ShippingCheckoutStep
        {
            AddressId = saved?.AddressId,
            Address = address,
            RateId = rate.RateId,
            SaveAddress = saved is null && customer is not null && model.SaveAddress,
        }, address, rate, stockLocationId: null);

        await _invoiceBuilder.BuildAsync(flow);

        return BuildEditor(flow, posted: null, quote);
    }

    // Keeps the buyer's choice, where the order goes and how, and adds what it costs to the invoice.
    private void Choose(ICheckoutFlowSession session, ShippingCheckoutStep step, Address address, ShippingRate rate, string stockLocationId)
    {
        session.SavedSteps[ShippingConstants.CheckoutStepKey] = JObject.FromObject(step);

        session.Put(new CheckoutShippingInfo
        {
            Address = address,
            RateId = rate.RateId,
            MethodId = rate.MethodId,
            ZoneId = rate.ZoneId,
            ProviderName = rate.ProviderName,
            Name = rate.Name,
            Description = rate.Description,
            Amount = rate.Amount,
            LocationId = stockLocationId,
        });

        // Shipping is a line of the invoice, taxed as shipping where the order goes.
        if (session.Steps.FirstOrDefault(entry => string.Equals(entry.Key, ShippingConstants.CheckoutStepKey, StringComparison.Ordinal)) is { } shippingStep)
        {
            shippingStep.BillingItems =
            [
                new BillingItem
                {
                    ItemId = ShippingLineId,
                    Description = step.PickupLocationId is null ? S["Shipping: {0}", rate.Name] : rate.Name,
                    Amount = rate.Amount,
                    Quantity = 1,
                    TaxableKind = TaxableItemKind.Shipping,
                    TaxCategoryCode = rate.TaxCategoryCode,
                },
            ];
        }
    }

    private ShippingRate CreatePickupRate(PickupLocation location)
        => new()
        {
            RateId = PickupRateId(location),
            ProviderName = ShippingConstants.Providers.Pickup,
            Name = S["Pick up at {0}", location.Name],
            Description = location.Instructions,
            Amount = location.Fee,
        };

    private static string PickupRateId(PickupLocation location)
        => ShippingConstants.Providers.Pickup.ToLowerInvariant() + ":" + location.LocationId;

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
        => new()
        {
            ShowName = true,
            RequireName = true,
            ShowCompany = true,
            ShowPhone = true,
            DefaultCountry = GetBillingAddress(session)?.Country ?? customer?.GetDefaultBillingAddress()?.Address?.Country,
        };

    private static Address GetBillingAddress(ICheckoutFlowSession session)
        => session.TryGet<CheckoutContactInfo>(out var contact) ? contact.BillingAddress : null;

    // Enough to find the zone and price shipping: a country, and a first line to ship to.
    private static bool IsComplete(Address address)
        => !string.IsNullOrWhiteSpace(address?.Country) && !string.IsNullOrWhiteSpace(address.AddressLine1);

    private async Task<Order> GetOrderAsync(ICheckoutFlowSession session)
        => session is CheckoutSession { ReferenceId: { Length: > 0 } orderId } ? await _orderStore.FindByIdAsync(orderId) : null;

    private async Task<Customer> GetCustomerAsync(ICheckoutFlowSession session)
        => string.IsNullOrEmpty(session.OwnerId) || _serviceProvider.GetService<ICustomerService>() is not { } customers
            ? null
            : await customers.GetOrCreateForUserAsync(session.OwnerId);
}
