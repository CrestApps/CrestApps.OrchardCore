using System.Globalization;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// A booking on a cart line: its date (or dates) and occupancy are checked when it's added, it's priced for its dates
/// and the people in it, it can't be bought beyond what's left on its dates, and it reads as when and for whom.
/// </summary>
internal sealed class BookingCartLineHandler : ICartLineHandler
{
    private readonly BookingCapacityService _capacity;

    internal readonly IStringLocalizer S;

    public BookingCartLineHandler(BookingCapacityService capacity, IStringLocalizer<BookingCartLineHandler> stringLocalizer)
    {
        _capacity = capacity;
        S = stringLocalizer;
    }

    // Before add-ons, which can be priced per person.
    public int Order
        => 10;

    public async Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default)
    {
        if (context.ContentItem?.TryGet<BookingPart>(out var part) != true)
        {
            return;
        }

        context.PostedDetails.TryGetValue(BookingsConstants.Details.Date, out var date);
        context.PostedDetails.TryGetValue(BookingsConstants.Details.End, out var end);
        context.PostedDetails.TryGetValue(BookingsConstants.Details.Occupancy, out var occupancyId);

        if (string.IsNullOrWhiteSpace(date))
        {
            context.Refuse(part.Kind switch
            {
                BookingKind.Nightly => S["Choose your dates for {0}.", context.Title],
                BookingKind.TimeSlot => S["Choose a time for {0}.", context.Title],
                _ => S["Choose a date for {0}.", context.Title],
            });

            return;
        }

        date = date.Trim();
        end = part.Kind == BookingKind.Nightly ? end?.Trim() : null;

        switch (BookingSchedule.Check(part, date, end, await _capacity.GetLocalNowAsync()))
        {
            case "past":
                context.Refuse(S["That date has passed. Choose another for {0}.", context.Title]);
                return;
            case "far":
                context.Refuse(S["{0} can be booked up to {1} days ahead.", context.Title, part.DaysAhead]);
                return;
            case "nights":
                context.Refuse(S["A stay at {0} is {1} to {2} nights.", context.Title, Math.Max(1, part.MinNights), Math.Max(part.MinNights, part.MaxNights)]);
                return;
            case "closed":
                context.Refuse(S["{0} can't be booked then. Choose another date.", context.Title]);
                return;
        }

        var occupancies = GetOccupancies(part);

        if (occupancies.Count > 0)
        {
            var occupancy = occupancies.FirstOrDefault(candidate => string.Equals(candidate.Id, occupancyId, StringComparison.Ordinal)) ??
                (occupancies.Count == 1 ? occupancies[0] : null);

            if (occupancy is null)
            {
                context.Refuse(S["Choose an option for {0}, such as a room.", context.Title]);

                return;
            }

            context.Details[BookingsConstants.Details.Occupancy] = occupancy.Id;
        }

        context.Details[BookingsConstants.Details.Date] = date;

        if (!string.IsNullOrEmpty(end))
        {
            context.Details[BookingsConstants.Details.End] = end;
        }
    }

    public async Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default)
    {
        if (context.ContentItem?.TryGet<BookingPart>(out var part) != true)
        {
            return;
        }

        var keys = BookingSchedule.GetSlotKeys(part.Kind, context.GetDetail(BookingsConstants.Details.Date), context.GetDetail(BookingsConstants.Details.End));

        if (keys.Count == 0)
        {
            context.Problem ??= S["Choose a date for {0}.", context.Title];

            return;
        }

        var occupancy = Find(part, context.GetDetail(BookingsConstants.Details.Occupancy));
        var people = Math.Max(1, occupancy?.People ?? 1);

        context.PeoplePerUnit = people;

        // Each date at its own price, a weekend night at the weekend price, or else the product's.
        var slots = await _capacity.ListSlotsAsync(context.ContentItem.ContentItemId, keys, cancellationToken);
        var weekendPrice = part.Kind == BookingKind.Nightly && part.WeekendPrice is { } weekend ? await context.ConvertAsync(weekend, cancellationToken) : (decimal?)null;
        var prices = new List<decimal>();

        foreach (var key in keys)
        {
            var slot = slots.FirstOrDefault(candidate => string.Equals(candidate.SlotKey, key, StringComparison.Ordinal));

            prices.Add(slot?.Price is { } own
                ? await context.ConvertAsync(own, cancellationToken)
                : weekendPrice is { } price && BookingSchedule.IsWeekendNight(key) ? price : context.UnitPrice);
        }

        var adjustment = occupancy is null ? 0m : await context.ConvertAsync(occupancy.PriceAdjustment, cancellationToken);

        context.UnitPrice = BookingSchedule.GetUnitPrice(part, people, prices, adjustment);

        // Only what's left on the fullest of its dates can be bought.
        var seats = BookingSchedule.GetSeatsPerUnit(part.Kind, people);
        var remaining = await _capacity.GetRemainingAsync(context.ContentItem.ContentItemId, part, keys, context.CartId, cancellationToken);

        context.MaxQuantity = Math.Min(context.MaxQuantity, remaining / seats);

        if (remaining < seats)
        {
            context.Problem ??= remaining == 0
                ? S["{0} is fully booked then. Choose another date.", context.Title]
                : S["Only {0} places are left on {1} then.", remaining, context.Title];
        }

        context.Details.Add(new CartLineDetail(BookingsConstants.Details.Date, GetDateLabel(part.Kind), Describe(part.Kind, keys)));

        if (occupancy is not null)
        {
            context.Details.Add(new CartLineDetail(BookingsConstants.Details.Occupancy, null, people > 1 ? S["{0} ({1} people)", occupancy.Label, people].Value : occupancy.Label));
        }
    }

    internal static IReadOnlyList<BookingOccupancy> GetOccupancies(BookingPart part)
        => [.. (part?.Occupancies ?? []).Where(occupancy => !string.IsNullOrEmpty(occupancy.Id) && !string.IsNullOrWhiteSpace(occupancy.Label))];

    internal static BookingOccupancy Find(BookingPart part, string occupancyId)
    {
        var occupancies = GetOccupancies(part);

        return occupancies.FirstOrDefault(candidate => string.Equals(candidate.Id, occupancyId, StringComparison.Ordinal)) ??
            (occupancies.Count == 1 ? occupancies[0] : null);
    }

    private string GetDateLabel(BookingKind kind)
        => kind switch
        {
            BookingKind.Nightly => S["Stay"],
            BookingKind.TimeSlot => S["Time"],
            _ => S["Date"],
        };

    // "Tuesday, 1 December 2026", "1 Dec 2026 – 4 Dec 2026, 3 nights" or "1 Dec 2026, 09:00", in the shopper's language.
    private string Describe(BookingKind kind, IReadOnlyList<string> keys)
    {
        var culture = CultureInfo.CurrentCulture;
        var first = BookingSchedule.GetLocalStart(keys[0]).Value;

        return kind switch
        {
            BookingKind.Nightly => S["{0} – {1}, {2} nights", first.ToString("d MMM yyyy", culture), first.AddDays(keys.Count).ToString("d MMM yyyy", culture), keys.Count].Value,
            BookingKind.TimeSlot => first.ToString("ddd d MMM yyyy", culture) + ", " + first.ToString("t", culture),
            _ => first.ToString("D", culture),
        };
    }
}
