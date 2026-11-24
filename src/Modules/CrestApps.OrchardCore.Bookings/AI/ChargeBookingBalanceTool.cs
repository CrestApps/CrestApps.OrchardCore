using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Bookings.AI;

/// <summary>
/// Charges the balance still owed on a booking's order to the card its deposit was paid with, through the charge its
/// detail page uses (<see cref="BookingBalanceCharger.ChargeNowAsync"/>), which also retries a balance whose charges failed.
/// </summary>
/// <remarks>
/// Under the owner's money rules the amount and currency must be given and equal the balance owed, as a confirmation of
/// what is charged, and every call carries an idempotency key, kept a day per site and caller, so a repeated call is refused
/// instead of charging twice. A charge the provider accepted may still be settling; the answer says so.
/// </remarks>
internal sealed class ChargeBookingBalanceTool : BookingsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "chargeBookingBalance";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "bookingId": {
          "type": "string",
          "description": "The booking's id, from listBookings."
        },
        "amount": {
          "type": "number",
          "description": "The balance owed, exactly as getBooking's balance amount gives it. Another amount is refused."
        },
        "currency": {
          "type": "string",
          "description": "The balance's currency, such as USD."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this charge, up to 128 characters, such as balance-1042. A second call with the same key is refused, so a retry never charges twice."
        }
      },
      "required": ["bookingId", "amount", "currency", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Charges the balance still owed on a booking to the card its deposit was paid with, now, including after an earlier failed attempt. The amount and currency must equal the balance owed, and every call needs a new idempotency key. Fails when nothing is owed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "bookingId") is not { } bookingId)
        {
            return "Nothing was charged: give the booking's id. Use listBookings to find it.";
        }

        if (await services.GetRequiredService<IBookingStore>().FindByIdAsync(bookingId, cancellationToken) is not { } booking)
        {
            return $"Nothing was charged: no booking has the id '{bookingId}'. Use listBookings to find it.";
        }

        var balances = services.GetRequiredService<IBookingBalanceStore>();

        if (string.IsNullOrEmpty(booking.OrderId) ||
            await balances.FindByOrderAsync(booking.OrderId, cancellationToken) is not { Status: BookingBalanceStatus.Scheduled or BookingBalanceStatus.Failed } balance)
        {
            return $"Nothing was charged: nothing is owed on booking '{booking.Title}'.";
        }

        var key = ReadString(arguments, "idempotencyKey");

        if (key is null || key.Length > MaxIdempotencyKeyLength)
        {
            return $"Nothing was charged: give a new idempotencyKey of up to {MaxIdempotencyKeyLength} characters for this charge.";
        }

        if (!TryReadDecimal(arguments, "amount", out var amount) || amount is null ||
            amount.Value != balance.Amount ||
            !string.Equals(ReadString(arguments, "currency"), balance.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was charged: the balance owed on booking '{booking.Title}' is {Format(balance.Amount, balance.Currency)}. Give exactly that amount and currency to charge it.";
        }

        var charger = services.GetRequiredService<BookingBalanceCharger>();
        var fingerprint = string.Join('|', balance.ItemId, balance.Amount.ToString(CultureInfo.InvariantCulture), balance.Currency);

        var outcome = await RunOnceAsync(services, caller, key, fingerprint, async token =>
        {
            string failure;

            try
            {
                failure = await charger.ChargeNowAsync(balance, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The card may already have been charged; the key is used up so a retry can't charge twice.
                await DiscardAsync(services);

                return $"The balance charge for booking '{booking.Title}' stopped with an error, and whether the card was charged is unknown. Do not retry with this or another key; check the booking's page and its order.";
            }

            if (failure is not null)
            {
                await CommitAsync(services, token);

                return Json(new
                {
                    bookingId = booking.ItemId,
                    charged = false,
                    amount = balance.Amount,
                    currency = balance.Currency,
                    message = $"The card wasn't charged: {failure}",
                });
            }

            var saveError = await CommitAsync(services, token);

            return Json(new
            {
                bookingId = booking.ItemId,
                orderNumber = balance.OrderNumber,
                charged = true,
                amount = balance.Amount,
                currency = balance.Currency,
                recordSaved = saveError is null,
                message = saveError is null
                    ? $"The payment provider accepted the charge of {Format(balance.Amount, balance.Currency)} for booking '{booking.Title}'; it may still be settling. The balance is recorded as paid."
                    : $"The payment provider may have charged {Format(balance.Amount, balance.Currency)}, but the balance's record was not saved because it changed at the same moment. Do not retry; check the booking's page and its order.",
            });
        }, cancellationToken);

        if (outcome is null)
        {
            return "Nothing was charged: the site can't keep idempotency keys right now, so a charge from a chat can't be guarded against running twice. Charge it from the booking's page.";
        }

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was charged again: the idempotency key '{key}' was already used for this charge. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was charged: the idempotency key '{key}' was already used for a different request. Use a new key.",
            _ => $"Nothing was charged: a charge with the idempotency key '{key}' is still running. Read the booking again with getBooking in your next message.",
        };
    }
}
