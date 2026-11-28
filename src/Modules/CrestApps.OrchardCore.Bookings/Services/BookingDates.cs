using System.Globalization;
using CrestApps.OrchardCore.Bookings.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// Adds a date or time slot to a bookable product, and changes an existing date's capacity, price or closed flag, with the
/// rules of the product's Dates page: a date is added once, a price is a number of at least zero or empty for the product's
/// own, and the capacity never goes below what is already held or sold. A date is closed, never deleted.
/// </summary>
public sealed class BookingDates
{
    /// <summary>
    /// The name of the new date's day field, as the Dates page names it.
    /// </summary>
    public const string DateField = "NewDate";

    /// <summary>
    /// The name of the new date's price field, as the Dates page names it.
    /// </summary>
    public const string PriceField = "NewPrice";

    private readonly IBookingSlotStore _slots;
    private readonly BookingCapacityService _capacity;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingDates"/> class.
    /// </summary>
    public BookingDates(
        IBookingSlotStore slots,
        BookingCapacityService capacity,
        IClock clock,
        IStringLocalizer<BookingDates> stringLocalizer)
    {
        _slots = slots;
        _capacity = capacity;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Adds a date, or a time slot on a day, to a bookable product.
    /// </summary>
    /// <param name="item">The bookable product.</param>
    /// <param name="date">The day, as <c>yyyy-MM-dd</c> in the store's booking time zone.</param>
    /// <param name="time">For a time-slot product, the slot's start, as <c>HH:mm</c>; otherwise ignored.</param>
    /// <param name="capacity">The date's capacity, or <see langword="null"/> for the product's.</param>
    /// <param name="price">The date's price as typed, or empty for the product's.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The date added, or the errors by field when it was not.</returns>
    public async Task<(BookingSlot Slot, IReadOnlyList<KeyValuePair<string, string>> Errors)> AddAsync(ContentItem item, string date, string time, int? capacity, string price, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var errors = new List<KeyValuePair<string, string>>();
        var part = item.TryGet<BookingPart>(out var bookingPart) ? bookingPart : new BookingPart();
        var key = part.Kind == BookingKind.TimeSlot && !string.IsNullOrWhiteSpace(time)
            ? date?.Trim() + "T" + time.Trim()
            : date?.Trim();
        var startsUtc = BookingSchedule.GetStartUtc(key, await _capacity.GetTimeZoneAsync());

        if (!TryParsePrice(price, out var parsedPrice))
        {
            errors.Add(new(PriceField, S["Enter the price as a number, such as 1200, or leave it empty for the product's."]));
        }

        if (startsUtc is null)
        {
            errors.Add(new(DateField, S["Choose the date."]));
        }
        else if (await _slots.FindAsync(item.ContentItemId, key, cancellationToken) is not null)
        {
            errors.Add(new(DateField, S["That date is there already. Change it below."]));
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var slot = new BookingSlot
        {
            ItemId = global::OrchardCore.IdGenerator.GenerateId(),
            ContentItemId = item.ContentItemId,
            SlotKey = key,
            StartsUtc = startsUtc.Value,
            Capacity = Math.Max(0, capacity ?? part.Capacity),
            Price = parsedPrice,
            ModifiedUtc = _clock.UtcNow,
        };

        await _slots.CreateAsync(slot, cancellationToken);

        return (slot, errors);
    }

    /// <summary>
    /// Changes a date's capacity, price and closed flag. What is held or sold already stays: the capacity can't go below it.
    /// </summary>
    /// <param name="slot">The date.</param>
    /// <param name="capacity">The capacity asked for.</param>
    /// <param name="price">The price as typed, or empty for the product's.</param>
    /// <param name="closed">Whether the date takes no more bookings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when it was changed, or why not.</returns>
    public async Task<string> UpdateAsync(BookingSlot slot, int capacity, string price, bool closed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (!TryParsePrice(price, out var parsedPrice))
        {
            return S["Enter the price as a number, such as 1200, or leave it empty for the product's."];
        }

        slot.Capacity = Math.Max(slot.Held + slot.Sold, Math.Max(0, capacity));
        slot.Price = parsedPrice;
        slot.Closed = closed;
        slot.ModifiedUtc = _clock.UtcNow;

        await _slots.UpdateAsync(slot, cancellationToken);

        return null;
    }

    // Empty means the product's own price; otherwise a number of at least zero.
    private static bool TryParsePrice(string text, out decimal? price)
    {
        price = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0m)
        {
            return false;
        }

        price = parsed;

        return true;
    }
}
