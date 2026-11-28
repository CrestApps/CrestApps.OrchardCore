using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Bookings.AI;

/// <summary>
/// Returns one booking as its detail page shows it: the booking, the balance still owed on its order, and, while it can
/// still be canceled, the cancellation quote (<see cref="BookingService.QuoteCancellationAsync"/>). A request's buyer is
/// read from its order, as the page does. Travelers are counted, not returned; their details stay on the page.
/// </summary>
internal sealed class GetBookingTool : BookingsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getBooking";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "bookingId": {
          "type": "string",
          "description": "The booking's id, from listBookings."
        }
      },
      "required": ["bookingId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one booking with its customer, travelers count, the balance still owed and when it is due, and the cancellation quote (fee percent, amount paid, refund the policy gives). Use it before canceling or charging.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "bookingId") is not { } bookingId)
        {
            return "Nothing was read: give the booking's id. Use listBookings to find it.";
        }

        if (await services.GetRequiredService<IBookingStore>().FindByIdAsync(bookingId, cancellationToken) is not { } booking)
        {
            return $"Nothing was read: no booking has the id '{bookingId}'. Use listBookings to find it.";
        }

        var customerName = booking.CustomerName;
        var customerEmail = booking.CustomerEmail;

        // A request is booked before its buyer is named on the order; the order says who they are.
        if (string.IsNullOrEmpty(customerEmail) &&
            !string.IsNullOrEmpty(booking.OrderId) &&
            await services.GetRequiredService<IOrderStore>().FindByIdAsync(booking.OrderId, cancellationToken) is { } order)
        {
            customerName ??= order.CustomerName;
            customerEmail = order.CustomerEmail;
        }

        var timeZone = await services.GetRequiredService<BookingCapacityService>().GetTimeZoneAsync();
        var balance = string.IsNullOrEmpty(booking.OrderId)
            ? null
            : await services.GetRequiredService<IBookingBalanceStore>().FindByOrderAsync(booking.OrderId, cancellationToken);
        var quote = booking.Status is BookingStatus.Confirmed or BookingStatus.Requested or BookingStatus.Held
            ? await services.GetRequiredService<BookingService>().QuoteCancellationAsync(booking, cancellationToken)
            : null;

        return Json(new
        {
            id = booking.ItemId,
            title = booking.Title,
            productId = booking.ContentItemId,
            kind = ToCamel(booking.Kind.ToString()),
            dates = booking.SlotKeys,
            starts = BookingTime(booking.StartsUtc, timeZone),
            ends = BookingTime(booking.EndsUtc, timeZone),
            timeZone = timeZone.Id,
            units = booking.Units,
            people = booking.People,
            seats = booking.Seats,
            option = booking.OccupancyLabel,
            status = ToCamel(booking.Status.ToString()),
            holdExpires = BookingTime(booking.HoldExpiresUtc, timeZone),
            customerName,
            customerEmail,
            travelers = booking.Travelers?.Count ?? 0,
            total = booking.Total,
            currency = booking.Currency,
            orderId = booking.OrderId,
            orderNumber = booking.OrderNumber,
            confirmed = BookingTime(booking.ConfirmedUtc, timeZone),
            completed = BookingTime(booking.CompletedUtc, timeZone),
            canceled = BookingTime(booking.CanceledUtc, timeZone),
            cancellationFee = booking.CancellationFee,
            refunded = booking.Refunded,
            cancelReason = booking.CancelReason,
            balance = balance is null
                ? null
                : new
                {
                    amount = balance.Amount,
                    currency = balance.Currency,
                    due = BookingTime(balance.DueUtc, timeZone),
                    status = ToCamel(balance.Status.ToString()),
                    attempts = balance.Attempts,
                    nextAttempt = BookingTime(balance.NextAttemptUtc, timeZone),
                    lastFailure = balance.LastFailure,
                    canCharge = balance.Status is BookingBalanceStatus.Scheduled or BookingBalanceStatus.Failed,
                },
            cancellation = quote is null
                ? null
                : new
                {
                    daysBefore = quote.DaysBefore,
                    feePercent = quote.FeePercent,
                    paid = quote.Paid,
                    policyRefund = quote.Refund,
                    currency = booking.Currency,
                    note = "cancelBooking refunds at most 'paid'; 'policyRefund' is what the cancellation policy gives back.",
                },
            adminPath = GetAdminPath(services, "BookingsDetail", new { area = BookingsConstants.Feature, itemId = booking.ItemId }),
        });
    }
}
