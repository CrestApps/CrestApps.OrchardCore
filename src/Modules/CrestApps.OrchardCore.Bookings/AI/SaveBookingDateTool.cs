using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Bookings.AI;

/// <summary>
/// Adds a date or time slot to a bookable product, or changes an existing date's capacity, price or closed flag, with the
/// rules of the product's Dates page (<see cref="BookingDates"/>): a date is added once, the capacity never goes below what
/// is already held or sold, and a date is closed, never deleted. A change keeps the values not given.
/// </summary>
internal sealed class SaveBookingDateTool : BookingsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveBookingDate";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "dateId": {
          "type": "string",
          "description": "To change a date: its id, from listBookingDates. Leave it out to add a date."
        },
        "productId": {
          "type": "string",
          "description": "To add a date: the bookable product's content item id."
        },
        "date": {
          "type": "string",
          "description": "To add a date: the day, as yyyy-MM-dd in the store's booking time zone."
        },
        "time": {
          "type": "string",
          "description": "To add a time slot to a time-slot product: its start, as HH:mm. Ignored for other products."
        },
        "capacity": {
          "type": "integer",
          "description": "How many can book the date. When adding, the product's capacity is used if left out. It never goes below what is already held or sold."
        },
        "price": {
          "type": "number",
          "description": "The date's own price, 0 or more, instead of the product's."
        },
        "clearPrice": {
          "type": "boolean",
          "description": "When changing a date, set to true to drop its own price so the product's price applies."
        },
        "closed": {
          "type": "boolean",
          "description": "When changing a date, true closes it to new bookings and false opens it again."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds a date or time slot to a bookable product with a capacity and optional price, or changes an existing date's capacity, price or closed flag. Capacity cannot go below what is already booked; a date is closed, never deleted.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadInt(arguments, "capacity", out var capacity) || capacity < 0)
        {
            return "Nothing was saved: capacity must be a whole number, 0 or more.";
        }

        if (!TryReadDecimal(arguments, "price", out var price) || price < 0m)
        {
            return "Nothing was saved: price must be a number, 0 or more.";
        }

        var priceText = price?.ToString(CultureInfo.InvariantCulture);
        var dates = services.GetRequiredService<BookingDates>();
        var timeZone = await services.GetRequiredService<BookingCapacityService>().GetTimeZoneAsync();

        if (ReadString(arguments, "dateId") is { } dateId)
        {
            if (await services.GetRequiredService<IBookingSlotStore>().FindByIdAsync(dateId, cancellationToken) is not { } slot)
            {
                return $"Nothing was saved: no booking date has the id '{dateId}'. Use listBookingDates to find it.";
            }

            if (price is not null && ReadBool(arguments, "clearPrice") == true)
            {
                return "Nothing was saved: give a price or clearPrice, not both.";
            }

            var asked = capacity ?? slot.Capacity;
            var newPrice = ReadBool(arguments, "clearPrice") == true
                ? null
                : priceText ?? slot.Price?.ToString(CultureInfo.InvariantCulture);

            if (await dates.UpdateAsync(slot, asked, newPrice, ReadBool(arguments, "closed") ?? slot.Closed, cancellationToken) is { } error)
            {
                return $"Nothing was saved: {error}";
            }

            if (await CommitAsync(services, cancellationToken) is { } saveError)
            {
                return saveError;
            }

            return Json(new
            {
                saved = "changed",
                date = Describe(slot, timeZone),
                message = slot.Capacity > asked
                    ? $"The date was changed. Its capacity is {slot.Capacity}, not {asked}, because that many are already held or sold."
                    : "The date was changed.",
            });
        }

        if (ReadString(arguments, "productId") is not { } productId)
        {
            return "Nothing was saved: give dateId to change a date, or productId and date to add one.";
        }

        if (await services.GetRequiredService<IContentManager>().GetAsync(productId, VersionOptions.Latest) is not { } item || !item.Has<BookingPart>())
        {
            return $"Nothing was saved: no bookable product has the id '{productId}'.";
        }

        var (added, errors) = await dates.AddAsync(item, ReadString(arguments, "date"), ReadString(arguments, "time"), capacity, priceText, cancellationToken);

        if (added is null)
        {
            return $"Nothing was saved: {string.Join(" ", errors.Select(entry => entry.Value))}";
        }

        if (await CommitAsync(services, cancellationToken) is { } addError)
        {
            return addError;
        }

        return Json(new
        {
            saved = "added",
            product = item.DisplayText,
            date = Describe(added, timeZone),
            message = $"The date {added.SlotKey} was added to '{item.DisplayText}'.",
        });
    }

    private static object Describe(BookingSlot slot, TimeZoneInfo timeZone)
        => new
        {
            id = slot.ItemId,
            productId = slot.ContentItemId,
            date = slot.SlotKey,
            starts = BookingTime(slot.StartsUtc, timeZone),
            timeZone = timeZone.Id,
            capacity = slot.Capacity,
            held = slot.Held,
            sold = slot.Sold,
            remaining = slot.Remaining,
            price = slot.Price,
            closed = slot.Closed,
        };
}
