using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.AI;

/// <summary>
/// Lists bookings with the query of Commerce › Bookings (<see cref="IBookingStore.PageAsync"/>): a search, a status, by
/// default only bookings from today on, and, beyond the page, a product and a date range. Without a status, unpaid holds
/// and expired ones are left out, as on the page. Dates are days in the store's booking time zone.
/// </summary>
internal sealed class ListBookingsTool : BookingsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listBookings";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text to find in the booking's title, customer or order number."
        },
        "status": {
          "type": "string",
          "enum": ["requested", "confirmed", "completed", "noShow", "canceled"],
          "description": "Only bookings in this status. Leave it out for every status except unpaid and expired holds."
        },
        "productId": {
          "type": "string",
          "description": "Only bookings of this bookable product (its content item id)."
        },
        "fromDate": {
          "type": "string",
          "description": "Only bookings starting on or after this day, as yyyy-MM-dd in the store's booking time zone."
        },
        "toDate": {
          "type": "string",
          "description": "Only bookings starting on or before this day, as yyyy-MM-dd in the store's booking time zone."
        },
        "anyDate": {
          "type": "boolean",
          "description": "Set to true to include past bookings. By default only bookings from today on are listed, unless fromDate is given."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Bookings per page, 20 by default and at most 50."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists bookings with optional search, status, product, date range and paging; by default only bookings from today on. Returns title, date, people, customer, total, status and order number.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadEnum<BookingStatus>(arguments, "status", out var status) || status is BookingStatus.Held or BookingStatus.Expired)
        {
            return "Nothing was listed: status must be requested, confirmed, completed, noShow or canceled.";
        }

        var timeZone = await services.GetRequiredService<BookingCapacityService>().GetTimeZoneAsync();
        DateTime? fromUtc = null;
        DateTime? toUtc = null;

        if (ReadString(arguments, "fromDate") is { } fromText)
        {
            if (!BookingSchedule.TryParseDate(fromText, out var from))
            {
                return "Nothing was listed: fromDate must be a day as yyyy-MM-dd.";
            }

            fromUtc = BookingSchedule.GetStartUtc(BookingSchedule.ToKey(from), timeZone);
        }

        if (ReadString(arguments, "toDate") is { } toText)
        {
            if (!BookingSchedule.TryParseDate(toText, out var to))
            {
                return "Nothing was listed: toDate must be a day as yyyy-MM-dd.";
            }

            toUtc = BookingSchedule.GetStartUtc(BookingSchedule.ToKey(to.AddDays(1)), timeZone);
        }

        var (page, pageSize) = ReadPaging(arguments);
        var query = new BookingQuery
        {
            Search = ReadString(arguments, "search"),
            Status = status,
            ContentItemId = ReadString(arguments, "productId"),
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Upcoming = fromUtc is null && ReadBool(arguments, "anyDate") != true,
        };

        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var (count, bookings) = await services.GetRequiredService<IBookingStore>().PageAsync(query, utcNow, (page - 1) * pageSize, pageSize, cancellationToken);

        return ListResult(bookings.Select(booking => (object)new
        {
            id = booking.ItemId,
            title = booking.Title,
            productId = booking.ContentItemId,
            starts = BookingTime(booking.StartsUtc, timeZone),
            ends = BookingTime(booking.EndsUtc, timeZone),
            timeZone = timeZone.Id,
            people = booking.People,
            option = booking.OccupancyLabel,
            customerName = booking.CustomerName,
            total = booking.Total,
            currency = booking.Currency,
            status = ToCamel(booking.Status.ToString()),
            orderId = booking.OrderId,
            orderNumber = booking.OrderNumber,
        }), count, page, pageSize);
    }
}
