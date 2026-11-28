using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Bookings.AI;

/// <summary>
/// Lists the dates or time slots of one bookable product, as its Dates page does (from yesterday on), or of every product
/// in a range of days, as the bookings calendar does (the current month by default), from
/// <see cref="IBookingSlotStore.ListAsync(string, DateTime, DateTime, CancellationToken)"/>: capacity, held, sold, what is
/// left, the date's own price and whether it is closed. Days and times are in the store's booking time zone.
/// </summary>
internal sealed class ListBookingDatesTool : BookingsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listBookingDates";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "productId": {
          "type": "string",
          "description": "The bookable product's content item id. Leave it out to list every product's dates in the range (the calendar view)."
        },
        "fromDate": {
          "type": "string",
          "description": "The first day, as yyyy-MM-dd in the store's booking time zone. Default: yesterday for one product, the first day of this month otherwise."
        },
        "toDate": {
          "type": "string",
          "description": "The last day, as yyyy-MM-dd. Default: 800 days ahead for one product, the end of the month otherwise."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Dates per page, 20 by default and at most 50."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the dates or time slots of one bookable product, or of every product in a date range (the calendar view): capacity, held, sold, remaining, price override, closed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var contentManager = services.GetRequiredService<IContentManager>();
        var productId = ReadString(arguments, "productId");
        ContentItem product = null;

        if (productId is not null &&
            ((product = await contentManager.GetAsync(productId, VersionOptions.Latest)) is null || !product.Has<BookingPart>()))
        {
            return $"Nothing was listed: no bookable product has the id '{productId}'.";
        }

        var capacity = services.GetRequiredService<BookingCapacityService>();
        var timeZone = await capacity.GetTimeZoneAsync();
        var today = DateOnly.FromDateTime(await capacity.GetLocalNowAsync());
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var from = product is null ? monthStart : today.AddDays(-1);
        var to = product is null ? monthStart.AddMonths(1).AddDays(-1) : today.AddDays(800);

        if (ReadString(arguments, "fromDate") is { } fromText && !BookingSchedule.TryParseDate(fromText, out from))
        {
            return "Nothing was listed: fromDate must be a day as yyyy-MM-dd.";
        }

        if (ReadString(arguments, "toDate") is { } toText && !BookingSchedule.TryParseDate(toText, out to))
        {
            return "Nothing was listed: toDate must be a day as yyyy-MM-dd.";
        }

        if (to < from)
        {
            return "Nothing was listed: toDate is before fromDate.";
        }

        var fromUtc = BookingSchedule.GetStartUtc(BookingSchedule.ToKey(from), timeZone).Value;
        var toUtc = BookingSchedule.GetStartUtc(BookingSchedule.ToKey(to.AddDays(1)), timeZone).Value;
        var slots = (await services.GetRequiredService<IBookingSlotStore>().ListAsync(product?.ContentItemId, fromUtc, toUtc, cancellationToken))
            .OrderBy(slot => slot.StartsUtc)
            .ToList();
        var (page, pageSize) = ReadPaging(arguments);
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        var items = new List<object>();

        foreach (var slot in slots.Skip((page - 1) * pageSize).Take(pageSize))
        {
            if (!titles.TryGetValue(slot.ContentItemId, out var title))
            {
                title = product?.ContentItemId == slot.ContentItemId
                    ? product.DisplayText
                    : (await contentManager.GetAsync(slot.ContentItemId, VersionOptions.Latest))?.DisplayText ?? slot.ContentItemId;
                titles[slot.ContentItemId] = title;
            }

            items.Add(new
            {
                id = slot.ItemId,
                productId = slot.ContentItemId,
                product = title,
                date = slot.SlotKey,
                starts = BookingTime(slot.StartsUtc, timeZone),
                capacity = slot.Capacity,
                held = slot.Held,
                sold = slot.Sold,
                remaining = slot.Remaining,
                price = slot.Price,
                closed = slot.Closed,
            });
        }

        return Json(new
        {
            items,
            total = slots.Count,
            page,
            pageSize,
            truncated = (page - 1) * pageSize + items.Count < slots.Count,
            fromDate = BookingSchedule.ToKey(from),
            toDate = BookingSchedule.ToKey(to),
            timeZone = timeZone.Id,
        });
    }
}
