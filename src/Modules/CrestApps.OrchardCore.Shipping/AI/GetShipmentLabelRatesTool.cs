using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Asks the default carrier provider what each carrier would charge for a pending shipment's label, as the Get rates
/// step of the shipment's label page: the parcel is weighed as the page weighs it (<see cref="ShipmentLabels"/>) unless a
/// weight is given, the ship-from address is the shipping settings' or the store's, and nothing is bought. The rates are
/// kept for 30 minutes for <c>buyShipmentLabel</c>.
/// </summary>
internal sealed class GetShipmentLabelRatesTool : ShippingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getShipmentLabelRates";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "shipmentId": {
          "type": "string",
          "description": "The pending shipment's id, from getOrderFulfillment."
        },
        "weight": {
          "type": "number",
          "description": "What the parcel weighs, in the shipping settings' weight unit. Left out, the items are weighed as the checkout weighed them, plus the packaging."
        },
        "length": {
          "type": "number",
          "description": "The parcel's length; left out, the default parcel size of the shipping settings."
        },
        "width": {
          "type": "number",
          "description": "The parcel's width; left out, the default parcel size."
        },
        "height": {
          "type": "number",
          "description": "The parcel's height; left out, the default parcel size."
        }
      },
      "required": ["shipmentId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Asks the default carrier provider (such as EasyPost) what each carrier would charge to send a pending shipment, from its weight and parcel size, and returns the rates with a quote id for buyShipmentLabel. It needs a ship-from address and the order's shipping address; nothing is bought.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadDecimal(arguments, "weight", out var weight) ||
            !TryReadDecimal(arguments, "length", out var length) ||
            !TryReadDecimal(arguments, "width", out var width) ||
            !TryReadDecimal(arguments, "height", out var height))
        {
            return "Nothing was quoted: weight, length, width and height must be numbers.";
        }

        var (shipment, error) = await FindShipmentAsync(services, arguments, cancellationToken);

        if (shipment is null)
        {
            return error;
        }

        if (shipment.Status != ShipmentStatus.Pending)
        {
            return $"Nothing was quoted: shipment {shipment.Number} has already left.";
        }

        var order = await services.GetRequiredService<IOrderStore>().FindByIdAsync(shipment.OrderId, cancellationToken);

        if (order?.ShippingAddress is null)
        {
            return "Nothing was quoted: the shipment's order has no shipping address.";
        }

        var carrier = await services.GetRequiredService<ICarrierProviderResolver>().GetDefaultAsync();

        if (carrier is null)
        {
            return "Nothing was quoted: choose a default carrier provider, such as EasyPost, in Settings › Commerce › Shipping to buy labels.";
        }

        var siteService = services.GetRequiredService<ISiteService>();
        var settings = await siteService.GetSettingsAsync<ShippingSettings>();
        var shipFrom = await ShipmentLabels.GetShipFromAsync(siteService, settings);

        if (shipFrom is null)
        {
            return "Nothing was quoted: set a ship-from address in Settings › Commerce › Shipping, or the store's address in the store profile.";
        }

        var parcelWeight = weight ?? await ShipmentLabels.WeighAsync(services.GetRequiredService<ShippingOrderRateService>(), shipment, order, settings, cancellationToken);

        if (parcelWeight <= 0m)
        {
            return "Nothing was quoted: give what the parcel weighs, in weight.";
        }

        var quote = await carrier.QuoteAsync(ShipmentLabels.BuildRequest(
            order,
            shipment,
            shipFrom,
            settings,
            parcelWeight,
            length ?? settings.DefaultParcelLength,
            width ?? settings.DefaultParcelWidth,
            height ?? settings.DefaultParcelHeight), cancellationToken);

        if (!quote.Succeeded)
        {
            return $"Nothing was quoted: the carrier provider answered: {quote.Error}";
        }

        var rates = quote.Rates
            .OrderBy(rate => rate.Amount)
            .Select(rate => new QuotedLabelRate
            {
                RateId = rate.RateId,
                Carrier = rate.Carrier,
                Service = rate.Service,
                Amount = rate.Amount,
                Currency = rate.Currency,
                DeliveryDays = rate.DeliveryDays,
            })
            .ToList();

        var kept = await ShipmentLabelQuotes.KeepAsync(services, new QuotedLabel
        {
            QuoteId = quote.QuoteId,
            ShipmentId = shipment.ItemId,
            Rates = rates,
        }, cancellationToken);

        return Json(new
        {
            shipmentId = shipment.ItemId,
            shipmentNumber = shipment.Number,
            orderNumber = order.OrderNumber,
            carrierProvider = carrier.DisplayName.Value,
            weight = parcelWeight,
            weightUnit = ToCamel(settings.WeightUnit.ToString()),
            quoteId = quote.QuoteId,
            rates = rates.Select(rate => new
            {
                rateId = rate.RateId,
                carrier = rate.Carrier,
                service = rate.Service,
                amount = rate.Amount,
                currency = rate.Currency,
                deliveryDays = rate.DeliveryDays,
            }),
            canBuy = kept,
            message = kept
                ? "Nothing was bought. To buy one, call buyShipmentLabel with the quote id, the rate id and its amount and currency, within 30 minutes."
                : "Nothing was bought. The site can't keep the quote right now, so buy the label on the shipment's label page.",
        });
    }
}
