using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// Buys a carrier label for a pending shipment, as the Buy step of the shipment's label page: the default carrier
/// provider buys the rate (<see cref="ICarrierProvider.BuyLabelAsync"/>), which costs money on the carrier account, then
/// the shipment is shipped with the label's carrier and tracking (<see cref="IShipmentService.ShipAsync"/>) and
/// committed. It buys only a rate <c>getShipmentLabelRates</c> quoted for that shipment in the last 30 minutes, at the
/// amount and currency given (quote then confirm), and every call carries an idempotency key so a retry never buys a
/// second label.
/// </summary>
internal sealed class BuyShipmentLabelTool : ShippingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "buyShipmentLabel";

    private const int MaxKeyLength = 128;

    private static readonly TimeSpan _keyLifetime = TimeSpan.FromHours(24);

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "shipmentId": {
          "type": "string",
          "description": "The pending shipment's id."
        },
        "quoteId": {
          "type": "string",
          "description": "The quote id getShipmentLabelRates returned for this shipment."
        },
        "rateId": {
          "type": "string",
          "description": "The id of the chosen rate of that quote."
        },
        "amount": {
          "type": "number",
          "description": "The chosen rate's price, as getShipmentLabelRates returned it. A different amount is refused."
        },
        "currency": {
          "type": "string",
          "description": "The chosen rate's currency."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this label, up to 128 characters. A second call with the same key is refused, so a retry never buys two labels."
        }
      },
      "required": ["shipmentId", "quoteId", "rateId", "amount", "currency", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Buys the chosen carrier label for a pending shipment from a quote of getShipmentLabelRates, at the rate's amount and currency, which costs money on the carrier account, then marks the shipment shipped with the label's carrier and tracking number and returns the label link. A bought label cannot be refunded from here. Every call needs a new idempotency key.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var key = ReadString(arguments, "idempotencyKey");

        if (key is null || key.Length > MaxKeyLength)
        {
            return $"Nothing was bought: give a new idempotencyKey of up to {MaxKeyLength} characters for this label.";
        }

        if (!TryReadDecimal(arguments, "amount", out var amount) || amount is not > 0m)
        {
            return "Nothing was bought: give the chosen rate's amount, as getShipmentLabelRates returned it.";
        }

        var (shipment, error) = await FindShipmentAsync(services, arguments, cancellationToken);

        if (shipment is null)
        {
            return error;
        }

        if (shipment.Status != ShipmentStatus.Pending)
        {
            return $"Nothing was bought: shipment {shipment.Number} has already left.";
        }

        var quoteId = ReadString(arguments, "quoteId");
        var rateId = ReadString(arguments, "rateId");
        var quote = quoteId is null ? null : await ShipmentLabelQuotes.FindAsync(services, quoteId, cancellationToken);

        if (quote is null || !string.Equals(quote.ShipmentId, shipment.ItemId, StringComparison.Ordinal))
        {
            return "Nothing was bought: that quote is not one getShipmentLabelRates gave for this shipment in the last 30 minutes. Get the rates again.";
        }

        var rate = quote.Rates.FirstOrDefault(entry => string.Equals(entry.RateId, rateId, StringComparison.Ordinal));

        if (rate is null)
        {
            return "Nothing was bought: the quote has no rate with that id. Use a rateId from getShipmentLabelRates.";
        }

        if (rate.Amount != amount.Value || !string.Equals(rate.Currency, ReadString(arguments, "currency"), StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was bought: the {rate.Carrier} {rate.Service} rate costs {Format(rate.Amount, rate.Currency)}, not what was given. Confirm that price.";
        }

        var carrier = await services.GetRequiredService<ICarrierProviderResolver>().GetDefaultAsync();
        var cache = services.GetService<IDistributedCache>();
        var locks = services.GetService<IDistributedLock>();

        if (carrier is null)
        {
            return "Nothing was bought: no default carrier provider is chosen in Settings › Commerce › Shipping.";
        }

        if (cache is null || locks is null)
        {
            return "Nothing was bought: the site can't keep idempotency keys right now, so a label bought from a chat can't be guarded against being bought twice. Buy it on the shipment's label page.";
        }

        var scope = $"{services.GetService<ShellSettings>()?.Name}|ai-tool:{TheName}|{caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.Identity?.Name}";
        var fingerprint = string.Join('|', shipment.ItemId, quoteId, rateId, amount.Value.ToString(CultureInfo.InvariantCulture), rate.Currency);

        var outcome = await IdempotentOperation.RunAsync(cache, locks, scope, key, fingerprint, _keyLifetime, async token =>
        {
            CarrierLabel label;

            try
            {
                label = await carrier.BuyLabelAsync(quoteId, rateId, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The carrier may have bought the label before the error; the key is used up so a retry can't buy twice.
                return new IdempotentOperationAnswer("Buying the label stopped with an error, and whether it was bought is unknown. Do not retry with this or another key; check the carrier account and the shipment's label page.", Keep: true);
            }

            if (!label.Succeeded)
            {
                // Nothing was bought, so the key may be used again.
                return new IdempotentOperationAnswer($"Nothing was bought: the carrier provider answered: {label.Error}", Keep: false);
            }

            // The label is paid for: from here on, whatever happens, the key stays used and the answer says so.
            var session = services.GetRequiredService<ISession>();
            var shipped = ShipmentResult.Fail(ShipmentError.InvalidStatus);
            var saved = true;

            try
            {
                shipment.LabelUrl = label.LabelUrl;
                shipment.LabelCost = label.Amount;
                shipment.LabelCurrency = label.Currency;

                shipped = await services.GetRequiredService<IShipmentService>().ShipAsync(shipment, new ShipmentDetails(label.Carrier, label.TrackingNumber, label.TrackingUrl), token);
                await session.SaveChangesAsync(token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await session.CancelAsync();
                saved = false;
            }

            return new IdempotentOperationAnswer(Json(new
            {
                shipmentId = shipment.ItemId,
                shipmentNumber = shipment.Number,
                bought = true,
                carrier = label.Carrier,
                service = label.Service,
                cost = label.Amount,
                currency = label.Currency,
                trackingNumber = label.TrackingNumber,
                trackingUrl = label.TrackingUrl,
                labelUrl = label.LabelUrl,
                shipmentShipped = saved && shipped.Succeeded,
                message = !saved
                    ? $"The label was bought and paid for on the carrier account, but the shipment was not updated (it changed at the same moment, or saving failed). Its tracking number is {label.TrackingNumber}; enter it on the shipment with shipShipment. Do not buy another label."
                    : shipped.Succeeded
                        ? $"Label bought: {label.Carrier}, tracking number {label.TrackingNumber}. The shipment is marked shipped; print the label from its link."
                        : $"Label bought: {label.Carrier}, tracking number {label.TrackingNumber}, but the shipment could not be marked shipped. Mark it with shipShipment.",
            }), Keep: true);
        }, cancellationToken);

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was bought again: the idempotency key '{key}' was already used for this label. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was bought: the idempotency key '{key}' was already used for a different label. Use a new key for a new label.",
            _ => $"Nothing was bought: a label with the idempotency key '{key}' is still being bought. Read the shipment again with getOrderFulfillment in your next message.",
        };
    }
}
