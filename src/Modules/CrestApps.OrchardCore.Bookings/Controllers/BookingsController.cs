using System.Globalization;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Bookings.ViewModels;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Bookings.Controllers;

/// <summary>
/// The storefront's booking pages: a search for what has room on the dates a shopper picks, and the request a buyer
/// sends for bookings the store confirms first.
/// </summary>
public sealed class BookingsController : Controller
{
    private const int MaxResults = 50;

    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public BookingsController(IShapeFactory shapeFactory, INotifier notifier, IHtmlLocalizer<BookingsController> htmlLocalizer)
    {
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpGet("bookings", Name = "BookingSearch")]
    public async Task<IActionResult> Search(
        string from,
        string to,
        int people,
        [FromServices] IContentDefinitionManager contentDefinitionManager,
        [FromServices] IContentManager contentManager,
        [FromServices] BookingCapacityService capacity,
        [FromServices] IBookingSlotStore slots,
        [FromServices] ISession session)
    {
        people = Math.Clamp(people, 1, 50);

        var results = new List<BookingSearchResult>();
        var searched = BookingSchedule.TryParseDate(from, out var fromDate);

        if (searched)
        {
            var toDate = BookingSchedule.TryParseDate(to, out var parsedTo) && parsedTo > fromDate ? parsedTo : fromDate.AddDays(1);
            var timeZone = await capacity.GetTimeZoneAsync();
            var types = (await contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(type => type.Parts.Any(part => part.PartDefinition.Name == nameof(BookingPart)))
                .Select(type => type.Name)
                .ToArray();
            var items = types.Length == 0
                ? []
                : await session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType.IsIn(types)).Take(500).ListAsync(HttpContext.RequestAborted);

            foreach (var item in items)
            {
                if (results.Count >= MaxResults || !item.TryGet<BookingPart>(out var part))
                {
                    continue;
                }

                var (key, end, remaining) = await FindAsync(item, part, fromDate, toDate, people, timeZone, capacity, slots);

                if (key is null)
                {
                    continue;
                }

                var metadata = await contentManager.PopulateAspectAsync<ContentItemMetadata>(item);
                var values = metadata.DisplayRouteValues is null ? null : new Microsoft.AspNetCore.Routing.RouteValueDictionary(metadata.DisplayRouteValues) { ["date"] = key };

                if (values is not null && end is not null)
                {
                    values["end"] = end;
                }

                var start = BookingSchedule.GetLocalStart(key).Value;

                results.Add(new BookingSearchResult(
                    item.ContentItemId,
                    item.DisplayText,
                    values is null ? null : Url.RouteUrl(values),
                    key,
                    part.Kind == BookingKind.TimeSlot ? start.ToString("f", CultureInfo.CurrentCulture) : start.ToString("D", CultureInfo.CurrentCulture),
                    remaining));
            }
        }

        return View(await _shapeFactory.CreateAsync<BookingSearchViewModel>("BookingSearch", model =>
        {
            model.From = from;
            model.To = to;
            model.People = people;
            model.Searched = searched;
            model.Results = [.. results.OrderBy(result => result.FirstDate, StringComparer.Ordinal)];
        }));
    }

    [HttpGet("bookings/request", Name = "BookingRequest")]
    public async Task<IActionResult> RequestBooking()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        return View("Request", await _shapeFactory.CreateAsync<BookingRequestViewModel>("BookingRequest", _ => { }));
    }

    [HttpPost("bookings/request")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(RequestBooking))]
    public async Task<IActionResult> RequestBookingPost(BookingRequestViewModel model, [FromServices] IServiceProvider services)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (services.GetService(typeof(IOrderRequestService)) is not IOrderRequestService requests)
        {
            return NotFound();
        }

        var (order, problems) = await requests.RequestAsync(User, model?.Message, HttpContext.RequestAborted);

        if (order is null)
        {
            return View("Request", await _shapeFactory.CreateAsync<BookingRequestViewModel>("BookingRequest", form =>
            {
                form.Message = model?.Message;
                form.Problems = problems;
            }));
        }

        await _notifier.SuccessAsync(H["Thanks: we've got your request, {0}. We'll email you when it's confirmed, with a link to pay.", order.OrderNumber]);

        return RedirectToRoute("AccountOrder", new { orderId = order.ItemId });
    }

    // The first date in the range with room for the people: a departure, a stay of the range's nights, or a time on the
    // first day.
    private static async Task<(string Key, string End, int Remaining)> FindAsync(ContentItem item, BookingPart part, DateOnly from, DateOnly to, int people, TimeZoneInfo timeZone, BookingCapacityService capacity, IBookingSlotStore slots)
    {
        var occupancy = BookingCartLineHandler.GetOccupancies(part).OrderByDescending(candidate => candidate.People).FirstOrDefault();
        var needed = part.Kind == BookingKind.Departure ? people : 1;
        var localNow = await capacity.GetLocalNowAsync();

        switch (part.Kind)
        {
            case BookingKind.Departure:
                var departures = await slots.ListAsync(item.ContentItemId, BookingSchedule.GetStartUtc(BookingSchedule.ToKey(from), timeZone).Value, BookingSchedule.GetStartUtc(BookingSchedule.ToKey(to.AddDays(1)), timeZone).Value);
                var open = departures.FirstOrDefault(slot => !slot.Closed && slot.Remaining >= needed && DateOnly.FromDateTime(BookingSchedule.GetLocalStart(slot.SlotKey) ?? DateTime.MinValue) >= DateOnly.FromDateTime(localNow));

                return open is null ? (null, null, 0) : (open.SlotKey, null, open.Remaining);

            case BookingKind.Nightly:
                var checkIn = BookingSchedule.ToKey(from);
                var checkOut = BookingSchedule.ToKey(to);

                if (BookingSchedule.Check(part, checkIn, checkOut, localNow) is not null)
                {
                    return (null, null, 0);
                }

                // Rooms enough for the people, each holding the most an option does.
                var rooms = (int)Math.Ceiling(people / (double)Math.Max(1, occupancy?.People ?? 1));
                var left = await capacity.GetRemainingAsync(item.ContentItemId, part, BookingSchedule.GetSlotKeys(part.Kind, checkIn, checkOut), cartId: null);

                return left >= rooms ? (checkIn, checkOut, left) : (null, null, 0);

            default:
                foreach (var time in BookingSchedule.GetTimes(part, from).Where(time => time > localNow))
                {
                    var key = BookingSchedule.ToKey(time);

                    // Only times the product's rules offer, such as within how far ahead it books.
                    if (BookingSchedule.Check(part, key, null, localNow) is not null)
                    {
                        continue;
                    }
                    var free = await capacity.GetRemainingAsync(item.ContentItemId, part, [key], cartId: null);

                    if (free >= needed)
                    {
                        return (key, null, free);
                    }
                }

                return (null, null, 0);
        }
    }
}

public class BookingRequestViewModel
{
    public string Message { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IReadOnlyList<string> Problems { get; set; } = [];
}
