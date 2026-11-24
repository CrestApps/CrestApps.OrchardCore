using CrestApps.Core.AI;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Shipping.AI;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Shipping;

/// <summary>
/// Registers the shipping AI tools when the Shipping and AI features are on: the shipment and label tools need
/// <c>FulfillOrders</c>, as the Shipments card of an order and a shipment's label page; <c>testShippingRates</c> needs
/// <c>ManageShipping</c>, as Try your rates. Shipping, removing a shipment and buying a label ask for approval; buying a
/// label needs a quote of <c>getShipmentLabelRates</c>, its price and an idempotency key. Shipping zones are exposed
/// read-only to the generic record tools as the <c>shippingZone</c> kind: their methods are a list of objects the
/// editor replay cannot post yet.
/// </summary>
[Feature(ShippingConstants.Features.Shipping)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetOrderFulfillmentTool>(GetOrderFulfillmentTool.TheName)
            .WithTitle(S["Get Order Fulfillment"])
            .WithDescription(S["Returns, for one order, the lines and quantities still to ship and its shipments with number, status, lines, carrier, tracking number and link, tracking events, label cost and dates. Use it before creating or changing a shipment."])
            .WithCategory(S["Shipping"])
            .Selectable();

        services.AddCoreAITool<CreateShipmentTool>(CreateShipmentTool.TheName)
            .WithTitle(S["Create Shipment"])
            .WithDescription(S["Creates a shipment to pack for a paid order from some or all of the lines still to ship, with an optional note, and leaves it pending. Use shipShipment when it leaves."])
            .WithCategory(S["Shipping"])
            .Selectable();

        services.AddCoreAITool<ShipShipmentTool>(ShipShipmentTool.TheName)
            .WithTitle(S["Ship Shipment"])
            .WithDescription(S["Marks a pending shipment as shipped with its carrier, tracking number and optional tracking link; or, given an order instead, creates and ships in one step everything still to ship on it. The buyer is emailed that the order is on its way when shipped emails are on."])
            .WithCategory(S["Shipping"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<UpdateShipmentStatusTool>(UpdateShipmentStatusTool.TheName)
            .WithTitle(S["Update Shipment Status"])
            .WithDescription(S["Marks a shipped shipment as delivered, or as returned so its items count as not shipped and can be shipped again."])
            .WithCategory(S["Shipping"])
            .Selectable();

        services.AddCoreAITool<DeleteShipmentTool>(DeleteShipmentTool.TheName)
            .WithTitle(S["Delete Shipment"])
            .WithDescription(S["Removes a shipment that has not left yet and puts its items back among those still to ship."])
            .WithCategory(S["Shipping"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<GetShipmentLabelRatesTool>(GetShipmentLabelRatesTool.TheName)
            .WithTitle(S["Get Shipment Label Rates"])
            .WithDescription(S["Asks the default carrier provider (such as EasyPost) what each carrier would charge to send a pending shipment, from its weight and parcel size, and returns the rates with a quote id for buyShipmentLabel. It needs a ship-from address and the order's shipping address; nothing is bought."])
            .WithCategory(S["Shipping"])
            .Selectable();

        services.AddCoreAITool<BuyShipmentLabelTool>(BuyShipmentLabelTool.TheName)
            .WithTitle(S["Buy Shipment Label"])
            .WithDescription(S["Buys the chosen carrier label for a pending shipment from a quote of getShipmentLabelRates, at the rate's amount and currency, which costs money on the carrier account, then marks the shipment shipped with the label's carrier and tracking number and returns the label link. A bought label cannot be refunded from here. Every call needs a new idempotency key."])
            .WithCategory(S["Shipping"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<TestShippingRatesTool>(TestShippingRatesTool.TheName)
            .WithTitle(S["Test Shipping Rates"])
            .WithDescription(S["Shows which shipping zone a country and region fall in and what each of its methods would charge for a test parcel of a given item count, subtotal, weight and shipping class, without placing an order. Use it to check rates after changing zones or methods; with carrier rates it asks the carrier for a live quote."])
            .WithCategory(S["Shipping"])
            .Selectable();

        services.AddAIRecordKind<ShippingZone>("shippingZone", kind => kind
            .WithTitle(S["Shipping zone"])
            .WithDescription(S["a shipping zone: the countries and regions it covers (or the rest of the world), in order, with its shipping methods (flat rate, table rate, free shipping, local delivery or carrier rates) and their rate settings. Read-only here; zones and methods are changed on Commerce › Shipping or with the ShippingZones recipe step"])
            .WithPermission(ShippingPermissions.ManageShipping)
            .UseCatalogManager<INamedCatalogManager<ShippingZone>>()
            .WithProjection(zone => new
            {
                id = zone.ItemId,
                name = zone.Name,
                position = zone.Position,
                countries = zone.Countries,
                regions = zone.Regions,
                restOfWorld = zone.RestOfWorld,
                methods = zone.Methods
                    .OrderBy(method => method.Position)
                    .Select(method => new
                    {
                        id = method.MethodId,
                        name = method.Name,
                        description = method.Description,
                        provider = method.ProviderName,
                        enabled = method.Enabled,
                        position = method.Position,
                        currency = method.Currency,
                        taxCategoryCode = method.TaxCategoryCode,
                        classSurcharges = method.ClassSurcharges is { Count: > 0 } surcharges ? new Dictionary<string, decimal>(surcharges, StringComparer.OrdinalIgnoreCase) : null,
                        flatRate = method.TryGet<FlatRateSettings>(out var flat) ? new { amount = flat.Amount, perItemAmount = flat.PerItemAmount } : null,
                        tableRate = method.TryGet<TableRateSettings>(out var table)
                            ? new { basis = table.Basis.ToString(), maximum = table.Maximum, rows = table.Rows.Select(row => new { from = row.From, amount = row.Amount }) }
                            : null,
                        freeShipping = method.TryGet<FreeShippingSettings>(out var free) ? new { minimumSubtotal = free.MinimumSubtotal } : null,
                        localDelivery = method.TryGet<LocalDeliverySettings>(out var local)
                            ? new { postalCodes = local.PostalCodes, amount = local.Amount, freeFromSubtotal = local.FreeFromSubtotal, minimumSubtotal = local.MinimumSubtotal }
                            : null,
                        carrierRates = method.TryGet<CarrierRateSettings>(out var carrier)
                            ? new { carriers = carrier.Carriers, maxRates = carrier.MaxRates, markupPercent = carrier.MarkupPercent, markupAmount = carrier.MarkupAmount, fallbackAmount = carrier.FallbackAmount }
                            : null,
                    }),
            })
            .ReadOnly());
    }
}
