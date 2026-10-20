using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// The checkout's Shipping step: where the order goes, or the shop it's picked up from, and how it's shipped. The
/// step's page and the commerce API both save it through here.
/// </summary>
public sealed class ShippingCheckoutStepService
{
    private const string ShippingLineId = "shipping";

    private readonly IOrderStore _orderStore;
    private readonly ShippingOrderRateService _orderRates;
    private readonly IAddressEditorService _addressEditor;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public ShippingCheckoutStepService(
        IOrderStore orderStore,
        ShippingOrderRateService orderRates,
        IAddressEditorService addressEditor,
        CheckoutInvoiceBuilder invoiceBuilder,
        IServiceProvider serviceProvider,
        ISiteService siteService,
        IStringLocalizer<ShippingCheckoutStepService> stringLocalizer)
    {
        _orderStore = orderStore;
        _orderRates = orderRates;
        _addressEditor = addressEditor;
        _invoiceBuilder = invoiceBuilder;
        _serviceProvider = serviceProvider;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the store's pickup locations.
    /// </summary>
    public async Task<IReadOnlyList<PickupLocation>> GetPickupLocationsAsync()
        => (await _siteService.GetSettingsAsync<ShippingSettings>()).PickupLocations ?? [];

    /// <summary>
    /// Returns the ways to ship the checkout's order to <paramref name="address"/>, or <see langword="null"/> when the
    /// order is gone.
    /// </summary>
    public async Task<ShippingQuote> QuoteAsync(CheckoutFlow flow, Address address)
        => await GetOrderAsync(flow.Session) is { } order ? await _orderRates.QuoteAsync(order, address) : null;

    /// <summary>
    /// Saves where the order goes and how, when what was entered is valid and a way to ship was chosen, and rebuilds
    /// the invoice with the shipping line. Problems are added to <paramref name="modelState"/>, keyed under
    /// <paramref name="prefix"/>; the result carries the ways to ship to the address, for the buyer to choose from.
    /// </summary>
    public async Task<ShippingStepResult> SaveAsync(CheckoutFlow flow, ShippingStepInput model, ModelStateDictionary modelState, string prefix)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(modelState);

        var session = flow.Session;
        var order = await GetOrderAsync(session);

        if (order is null)
        {
            modelState.AddModelError(Key(prefix, nameof(model.RateId)), S["This order can no longer be found. Go back to your cart and check out again."]);

            return ShippingStepResult.NotSaved(quote: null);
        }

        // Picked up from one of the store's locations: no address to enter and nothing to quote, the location is where
        // the order goes and what it's taxed by.
        if (!string.IsNullOrEmpty(model.PickupLocationId))
        {
            var location = (await _siteService.GetSettingsAsync<ShippingSettings>()).PickupLocations?
                .FirstOrDefault(entry => string.Equals(entry.LocationId, model.PickupLocationId, StringComparison.Ordinal));

            if (location is null)
            {
                modelState.AddModelError(Key(prefix, nameof(model.PickupLocationId)), S["That pickup location is no longer offered. Choose another, or have the order shipped."]);

                return ShippingStepResult.NotSaved(quote: null);
            }

            var pickupAddress = location.ToAddress();

            Choose(session, new ShippingCheckoutStep
            {
                PickupLocationId = location.LocationId,
                Address = pickupAddress,
                RateId = PickupRateId(location),
            }, pickupAddress, CreatePickupRate(location), location.StockLocationId);

            await _invoiceBuilder.BuildAsync(flow);

            return ShippingStepResult.Done(quote: null);
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
            var result = await _addressEditor.ValidateAsync(model.Address ?? new AddressEditorViewModel(), modelState, Key(prefix, nameof(model.Address)), EditorOptions(session, customer));

            if (result.IsValid)
            {
                address = result.Address;
            }
        }

        if (address is null || !modelState.IsValid)
        {
            return ShippingStepResult.NotSaved(quote: null);
        }

        var quote = await _orderRates.QuoteAsync(order, address);
        var choosing = false;

        if (quote.Zone is null)
        {
            modelState.AddModelError(Key(prefix, nameof(model.RateId)), S["We don't ship to this address. Choose another address."]);

            return ShippingStepResult.NotSaved(quote, choosing);
        }

        if (quote.Rates.Count == 0)
        {
            modelState.AddModelError(Key(prefix, nameof(model.RateId)), S["There is no way to ship this order to this address. Choose another address, or contact us."]);

            return ShippingStepResult.NotSaved(quote, choosing);
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
                modelState.AddModelError(Key(prefix, "Rates"), string.Empty);
                choosing = true;
            }
            else
            {
                modelState.AddModelError(Key(prefix, nameof(model.RateId)), S["That way to ship isn't offered for this address. Choose another."]);
            }

            return ShippingStepResult.NotSaved(quote, choosing);
        }

        Choose(session, new ShippingCheckoutStep
        {
            AddressId = saved?.AddressId,
            Address = address,
            RateId = rate.RateId,
            SaveAddress = saved is null && customer is not null && model.SaveAddress,
        }, address, rate, stockLocationId: null);

        await _invoiceBuilder.BuildAsync(flow);

        return ShippingStepResult.Done(quote);
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


    /// <summary>
    /// Returns the options the shipping address is entered with: who receives the parcel and a phone the carrier can
    /// call.
    /// </summary>
    public static AddressEditorOptions EditorOptions(ICheckoutFlowSession session, Customer customer)
        => new()
        {
            ShowName = true,
            RequireName = true,
            ShowCompany = true,
            ShowPhone = true,
            DefaultCountry = (session.TryGet<CheckoutContactInfo>(out var contact) ? contact.BillingAddress : null)?.Country ?? customer?.GetDefaultBillingAddress()?.Address?.Country,
        };

    public async Task<Order> GetOrderAsync(ICheckoutFlowSession session)
        => session is CheckoutSession { ReferenceId: { Length: > 0 } orderId } ? await _orderStore.FindByIdAsync(orderId) : null;

    public async Task<Customer> GetCustomerAsync(ICheckoutFlowSession session)
        => string.IsNullOrEmpty(session.OwnerId) || _serviceProvider.GetService<ICustomerService>() is not { } customers
            ? null
            : await customers.GetOrCreateForUserAsync(session.OwnerId);

    private static string Key(string prefix, string field)
        => string.IsNullOrEmpty(prefix) ? field : prefix + "." + field;
}

/// <summary>
/// What the buyer enters on the Shipping step.
/// </summary>
public sealed class ShippingStepInput
{
    /// <summary>
    /// Gets or sets the pickup location the order is collected from, or <see langword="null"/> when it's shipped.
    /// </summary>
    public string PickupLocationId { get; set; }

    /// <summary>
    /// Gets or sets a saved address of the signed-in buyer's to ship to, or <see langword="null"/> for a new one.
    /// </summary>
    public string AddressId { get; set; }

    public AddressEditorViewModel Address { get; set; }

    /// <summary>
    /// Gets or sets the way to ship chosen from the address's quote; not needed when there is only one.
    /// </summary>
    public string RateId { get; set; }

    public bool SaveAddress { get; set; }
}

/// <summary>
/// What saving the Shipping step came to.
/// </summary>
public sealed class ShippingStepResult
{
    public bool Saved { get; init; }

    /// <summary>
    /// Gets the ways to ship to the address entered, when it could be quoted.
    /// </summary>
    public ShippingQuote Quote { get; init; }

    /// <summary>
    /// Gets whether the ways to ship were just shown for the first time, for the buyer to choose one: not a mistake.
    /// </summary>
    public bool IsChoosing { get; init; }

    public static ShippingStepResult Done(ShippingQuote quote)
        => new() { Saved = true, Quote = quote };

    public static ShippingStepResult NotSaved(ShippingQuote quote, bool choosing = false)
        => new() { Quote = quote, IsChoosing = choosing };
}
