using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Bookings.AI;

/// <summary>
/// Cancels a booking through the cancel its detail page uses
/// (<see cref="BookingService.CancelAsync(Booking, decimal?, string, string, CancellationToken)"/>): it refunds the amount
/// given through the order, lets go of the balance still owed, frees the dates, notes it on the order and emails the buyer.
/// </summary>
/// <remarks>
/// Stricter than the page, under the owner's money rules: the page refunds the policy's amount when no amount is typed,
/// while this tool always passes the amount it was given (zero cancels without a refund), in the booking's currency, never
/// more than was paid toward the booking; refunding everything paid needs <c>refundAll</c>; and every call carries an
/// idempotency key, kept a day per site and caller, so a repeated call is refused instead of refunding twice. The change is
/// committed before the answer.
/// </remarks>
internal sealed class CancelBookingTool : BookingsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelBooking";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "bookingId": {
          "type": "string",
          "description": "The booking's id, from listBookings."
        },
        "refundAmount": {
          "type": "number",
          "description": "How much to give back, in the booking's currency: 0 for no refund, at most what was paid ('paid' in getBooking's cancellation). The policy's refund is 'policyRefund' there."
        },
        "currency": {
          "type": "string",
          "description": "The booking's currency, such as USD. Another currency is refused."
        },
        "refundAll": {
          "type": "boolean",
          "description": "Set to true to give back everything paid toward the booking; refundAmount must then be exactly that. An amount equal to everything paid needs it."
        },
        "reason": {
          "type": "string",
          "description": "Why the booking is canceled, kept with the booking and the refund."
        },
        "idempotencyKey": {
          "type": "string",
          "description": "A new unique key for this cancellation, up to 128 characters, such as cancel-booking-1042. A second call with the same key is refused, so a retry never refunds twice."
        }
      },
      "required": ["bookingId", "refundAmount", "currency", "idempotencyKey"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels a booking, refunds the buyer the stated amount and currency (0 for no refund, refundAll for everything paid), cancels any balance still owed, frees the dates and emails the buyer. Every call needs a new idempotency key; a repeated key is refused. Cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "bookingId") is not { } bookingId)
        {
            return "Nothing was canceled: give the booking's id. Use listBookings to find it.";
        }

        var store = services.GetRequiredService<IBookingStore>();

        if (await store.FindByIdAsync(bookingId, cancellationToken) is not { } booking)
        {
            return $"Nothing was canceled: no booking has the id '{bookingId}'. Use listBookings to find it.";
        }

        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Requested or BookingStatus.Held))
        {
            return $"Nothing was canceled: booking '{booking.Title}' is {ToCamel(booking.Status.ToString())} and can't be canceled any more.";
        }

        var key = ReadString(arguments, "idempotencyKey");

        if (key is null || key.Length > MaxIdempotencyKeyLength)
        {
            return $"Nothing was canceled: give a new idempotencyKey of up to {MaxIdempotencyKeyLength} characters for this cancellation.";
        }

        if (!TryReadDecimal(arguments, "refundAmount", out var amount) || amount is null || amount.Value < 0m)
        {
            return "Nothing was canceled: give refundAmount, 0 or more. Use 0 to cancel without a refund.";
        }

        if (!string.Equals(ReadString(arguments, "currency"), booking.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was canceled: booking '{booking.Title}' was paid in {booking.Currency}; give the refund in {booking.Currency}.";
        }

        if (Money.Round(amount.Value, booking.Currency) != amount.Value)
        {
            return $"Nothing was canceled: {amount.Value.ToString(CultureInfo.InvariantCulture)} has more decimal places than {booking.Currency} allows.";
        }

        var service = services.GetRequiredService<BookingService>();
        var quote = await service.QuoteCancellationAsync(booking, cancellationToken);
        var refundAll = ReadBool(arguments, "refundAll") == true;

        if (amount.Value > quote.Paid)
        {
            return $"Nothing was canceled: only {Format(quote.Paid, booking.Currency)} was paid toward booking '{booking.Title}', so no more can be refunded.";
        }

        if (refundAll && (amount.Value != quote.Paid || quote.Paid <= 0m))
        {
            return $"Nothing was canceled: everything paid toward booking '{booking.Title}' is {Format(quote.Paid, booking.Currency)}. Give that amount with refundAll, or a smaller amount without it.";
        }

        if (!refundAll && amount.Value > 0m && amount.Value == quote.Paid)
        {
            return $"Nothing was canceled: {Format(amount.Value, booking.Currency)} is everything paid toward booking '{booking.Title}'. Set refundAll to true to give it all back.";
        }

        var reason = ReadString(arguments, "reason");
        var fingerprint = string.Join('|', booking.ItemId, amount.Value.ToString(CultureInfo.InvariantCulture), booking.Currency, refundAll);

        var outcome = await RunOnceAsync(services, caller, key, fingerprint, async token =>
        {
            bool succeeded;
            IReadOnlyList<string> errors;

            try
            {
                (succeeded, errors) = await service.CancelAsync(booking, amount.Value, reason, GetActor(caller), token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Part of the refund may have reached the provider; the key is used up so a retry can't refund twice.
                await DiscardAsync(services);

                return $"The cancellation of booking '{booking.Title}' stopped with an error, and whether money was given back is unknown. Do not retry with this or another key; check the booking's page, its order and the Refunds page.";
            }

            if (!succeeded)
            {
                return Json(new
                {
                    bookingId = booking.ItemId,
                    canceled = false,
                    refunded = 0m,
                    currency = booking.Currency,
                    errors,
                    message = "Nothing was canceled and nothing was refunded; read the errors.",
                });
            }

            var saveError = await CommitAsync(services, token);

            // The refund commits what it changed and the booking is read again; read it once more for what was refunded.
            var canceled = await store.FindByIdAsync(booking.ItemId, token) ?? booking;
            var refunded = canceled.Refunded ?? 0m;

            return Json(new
            {
                bookingId = booking.ItemId,
                title = booking.Title,
                canceled = true,
                status = ToCamel(canceled.Status.ToString()),
                asked = amount.Value,
                refunded,
                currency = booking.Currency,
                recordSaved = saveError is null,
                message = saveError is not null
                    ? $"The refund may already have been given back ({Format(refunded, booking.Currency)}), but the booking's cancellation was not saved because it changed at the same moment. Do not retry; check the booking's page and its order."
                    : refunded > 0m
                        ? $"Booking '{booking.Title}' was canceled and {Format(refunded, booking.Currency)} was refunded through its order. The dates were freed, any balance owed was let go of, and the buyer was emailed."
                        : $"Booking '{booking.Title}' was canceled without a refund. The dates were freed, any balance owed was let go of, and the buyer was emailed.",
            });
        }, cancellationToken);

        if (outcome is null)
        {
            return "Nothing was canceled: the site can't keep idempotency keys right now, so a cancellation with a refund can't be guarded against running twice. Cancel it from the booking's page.";
        }

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was canceled again: the idempotency key '{key}' was already used for this cancellation. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was canceled: the idempotency key '{key}' was already used for a different request. Use a new key.",
            _ => $"Nothing was canceled: a cancellation with the idempotency key '{key}' is still running. Read the booking again with getBooking in your next message.",
        };
    }
}
