using System.Globalization;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Bookings.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Bookings.Controllers;

/// <summary>
/// Commerce › Bookings: what's booked, each booking's page with what can be done to it, a product's dates, and a
/// calendar.
/// </summary>
[Admin("bookings/{action}/{itemId?}", "Bookings{action}")]
public sealed class BookingsAdminController : Controller
{
    private readonly IBookingStore _bookings;
    private readonly IBookingSlotStore _slots;
    private readonly IBookingBalanceStore _balances;
    private readonly BookingService _service;
    private readonly BookingCapacityService _capacity;
    private readonly BookingBalanceCharger _charger;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public BookingsAdminController(
        IBookingStore bookings,
        IBookingSlotStore slots,
        IBookingBalanceStore balances,
        BookingService service,
        BookingCapacityService capacity,
        BookingBalanceCharger charger,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IClock clock,
        IHtmlLocalizer<BookingsAdminController> htmlLocalizer,
        IStringLocalizer<BookingsAdminController> stringLocalizer)
    {
        _bookings = bookings;
        _slots = slots;
        _balances = balances;
        _service = service;
        _capacity = capacity;
        _charger = charger;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("bookings", "Bookings")]
    public async Task<IActionResult> Index(string q, BookingStatus? status, string when, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        var upcoming = !string.Equals(when, "all", StringComparison.Ordinal);
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, bookings) = await _bookings.PageAsync(new BookingQuery { Search = q, Status = status, Upcoming = upcoming }, _clock.UtcNow, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        if (status is not null)
        {
            routeData.Values.TryAdd("status", status.ToString());
        }

        if (!upcoming)
        {
            routeData.Values.TryAdd("when", "all");
        }

        var model = new BookingsIndexViewModel
        {
            Search = q,
            Status = status,
            Upcoming = upcoming,
            List = new AdminListContext<Booking>
            {
                Source = bookings,
                Pager = (IShape)await _shapeFactory.PagerAsync(pager, count, routeData),
            },
        };

        // The filters render at the end of the list's toolbar.
        model.List.ToolbarActions = await _shapeFactory.CreateAsync("BookingsAdminListFilters", Arguments.From(new
        {
            Statuses = new List<SelectListItem>
            {
                new(S["Every booking"], string.Empty, status is null),
                new(S["Requested"], nameof(BookingStatus.Requested), status == BookingStatus.Requested),
                new(S["Confirmed"], nameof(BookingStatus.Confirmed), status == BookingStatus.Confirmed),
                new(S["Completed"], nameof(BookingStatus.Completed), status == BookingStatus.Completed),
                new(S["No-shows"], nameof(BookingStatus.NoShow), status == BookingStatus.NoShow),
                new(S["Canceled"], nameof(BookingStatus.Canceled), status == BookingStatus.Canceled),
            },
            Whens = new List<SelectListItem>
            {
                new(S["From today"], string.Empty, upcoming),
                new(S["Any date"], "all", !upcoming),
            },
        }));

        return View(model);
    }

    public async Task<IActionResult> Detail(string itemId, [FromServices] IContentItemDisplayManager displayManager, [FromServices] IUpdateModelAccessor updateModelAccessor, [FromServices] CrestApps.OrchardCore.Orders.Core.Services.IOrderStore orders)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        if (await _bookings.FindByIdAsync(itemId) is not { } booking)
        {
            return NotFound();
        }

        // A request is booked before its buyer is named on the order; the order says who they are.
        if (string.IsNullOrEmpty(booking.CustomerEmail) && await orders.FindByIdAsync(booking.OrderId, HttpContext.RequestAborted) is { } order)
        {
            booking.CustomerName ??= order.CustomerName;
            booking.CustomerEmail = order.CustomerEmail;
        }

        var model = new BookingDetailViewModel
        {
            Booking = booking,
            Balance = await _balances.FindByOrderAsync(booking.OrderId, HttpContext.RequestAborted),
            Cancellation = booking.Status is BookingStatus.Confirmed or BookingStatus.Requested or BookingStatus.Held
                ? await _service.QuoteCancellationAsync(booking, HttpContext.RequestAborted)
                : null,
            TimeZone = await _capacity.GetTimeZoneAsync(),
            OrderUrl = Url.RouteUrl("OrdersDetail", new { orderId = booking.OrderId }),
        };

        foreach (var traveler in booking.Travelers ?? [])
        {
            var item = await _contentManager.NewAsync(BookingsConstants.TravelerContentType);

            item.Merge(traveler.DeepClone());
            model.Travelers.Add(await displayManager.BuildDisplayAsync(item, updateModelAccessor.ModelUpdater, "Detail"));
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Complete(string itemId, bool noShow = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        if (await _bookings.FindByIdAsync(itemId) is not { } booking)
        {
            return NotFound();
        }

        if (await _service.FinishAsync(booking, noShow, User.Identity?.Name, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(noShow ? H["The booking was marked as a no-show."] : H["The booking was completed."]);
        }
        else
        {
            await _notifier.WarningAsync(H["Only a confirmed booking can be completed."]);
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(string itemId, string refund, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        if (await _bookings.FindByIdAsync(itemId) is not { } booking)
        {
            return NotFound();
        }

        decimal? amount = null;

        if (!string.IsNullOrWhiteSpace(refund))
        {
            if (!decimal.TryParse(refund.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0m)
            {
                await _notifier.ErrorAsync(H["Enter the refund as a number, such as 120.50."]);

                return RedirectToAction(nameof(Detail), new { itemId });
            }

            amount = parsed;
        }

        var (succeeded, errors) = await _service.CancelAsync(booking, amount, reason, User.Identity?.Name, HttpContext.RequestAborted);

        if (succeeded)
        {
            await _notifier.SuccessAsync(H["The booking was canceled, and the buyer was told."]);
        }
        else
        {
            foreach (var error in errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    [HttpPost]
    public async Task<IActionResult> ChargeBalance(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        if (await _bookings.FindByIdAsync(itemId) is not { } booking)
        {
            return NotFound();
        }

        if (await _balances.FindByOrderAsync(booking.OrderId, HttpContext.RequestAborted) is not { Status: BookingBalanceStatus.Scheduled or BookingBalanceStatus.Failed } balance)
        {
            await _notifier.WarningAsync(H["Nothing is owed on this booking."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        if (balance.Status == BookingBalanceStatus.Failed)
        {
            balance.Status = BookingBalanceStatus.Scheduled;
            balance.Attempts = 0;
        }

        if (await _charger.ChargeAsync(balance, HttpContext.RequestAborted) is { } failure)
        {
            await _notifier.ErrorAsync(H["The card wasn't charged: {0}", failure]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The balance was charged to the card."]);
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    public async Task<IActionResult> Dates(string contentItemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        if (await GetProductAsync(contentItemId) is not { } item)
        {
            return NotFound();
        }

        return View(await BuildDatesAsync(item, new BookingDatesViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(Dates))]
    public async Task<IActionResult> DatesPost(string contentItemId, BookingDatesViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        if (await GetProductAsync(contentItemId) is not { } item)
        {
            return NotFound();
        }

        var part = item.TryGet<BookingPart>(out var bookingPart) ? bookingPart : new BookingPart();
        var key = part.Kind == BookingKind.TimeSlot && !string.IsNullOrWhiteSpace(model.NewTime)
            ? model.NewDate?.Trim() + "T" + model.NewTime.Trim()
            : model.NewDate?.Trim();
        var startsUtc = BookingSchedule.GetStartUtc(key, await _capacity.GetTimeZoneAsync());
        decimal? price = null;

        if (!string.IsNullOrWhiteSpace(model.NewPrice))
        {
            if (!decimal.TryParse(model.NewPrice.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0m)
            {
                ModelState.AddModelError(nameof(model.NewPrice), S["Enter the price as a number, such as 1200, or leave it empty for the product's."]);
            }

            price = parsed;
        }

        if (startsUtc is null)
        {
            ModelState.AddModelError(nameof(model.NewDate), S["Choose the date."]);
        }
        else if (await _slots.FindAsync(item.ContentItemId, key) is not null)
        {
            ModelState.AddModelError(nameof(model.NewDate), S["That date is there already. Change it below."]);
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildDatesAsync(item, model));
        }

        await _slots.CreateAsync(new BookingSlot
        {
            ItemId = global::OrchardCore.IdGenerator.GenerateId(),
            ContentItemId = item.ContentItemId,
            SlotKey = key,
            StartsUtc = startsUtc.Value,
            Capacity = Math.Max(0, model.NewCapacity ?? part.Capacity),
            Price = price,
            ModifiedUtc = _clock.UtcNow,
        });

        await _notifier.SuccessAsync(H["The date was added."]);

        return RedirectToAction(nameof(Dates), new { contentItemId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateDate(string itemId, int capacity, string price, bool closed)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        if (await _slots.FindByIdAsync(itemId) is not { } slot)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(price) && (!decimal.TryParse(price.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0m))
        {
            await _notifier.ErrorAsync(H["Enter the price as a number, such as 1200, or leave it empty for the product's."]);

            return RedirectToAction(nameof(Dates), new { contentItemId = slot.ContentItemId });
        }

        // What's booked already stays booked: the capacity can't go below it.
        slot.Capacity = Math.Max(slot.Held + slot.Sold, Math.Max(0, capacity));
        slot.Price = string.IsNullOrWhiteSpace(price) ? null : decimal.Parse(price.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture);
        slot.Closed = closed;
        slot.ModifiedUtc = _clock.UtcNow;

        await _slots.UpdateAsync(slot);
        await _notifier.SuccessAsync(H["The date was changed."]);

        return RedirectToAction(nameof(Dates), new { contentItemId = slot.ContentItemId });
    }

    public async Task<IActionResult> Calendar(string month)
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        var timeZone = await _capacity.GetTimeZoneAsync();
        var today = DateOnly.FromDateTime(await _capacity.GetLocalNowAsync());
        var first = DateOnly.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var chosen)
            ? new DateOnly(chosen.Year, chosen.Month, 1)
            : new DateOnly(today.Year, today.Month, 1);
        var fromUtc = BookingSchedule.GetStartUtc(BookingSchedule.ToKey(first), timeZone).Value;
        var toUtc = BookingSchedule.GetStartUtc(BookingSchedule.ToKey(first.AddMonths(1)), timeZone).Value;
        var slots = await _slots.ListAsync(contentItemId: null, fromUtc, toUtc, HttpContext.RequestAborted);
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        var model = new BookingCalendarViewModel { Month = first };

        foreach (var slot in slots)
        {
            if (!titles.TryGetValue(slot.ContentItemId, out var title))
            {
                title = (await _contentManager.GetAsync(slot.ContentItemId, VersionOptions.Latest))?.DisplayText ?? slot.ContentItemId;
                titles[slot.ContentItemId] = title;
            }

            var start = BookingSchedule.GetLocalStart(slot.SlotKey) ?? BookingEmailSender.ToLocal(slot.StartsUtc, timeZone);
            var day = DateOnly.FromDateTime(start);

            if (!model.Days.TryGetValue(day, out var entries))
            {
                model.Days[day] = entries = [];
            }

            entries.Add(new BookingCalendarEntry(slot.ContentItemId, title, slot.SlotKey.Contains('T') ? start.ToString("t", CultureInfo.CurrentCulture) : null, slot.Held + slot.Sold, slot.Capacity, slot.Closed));
        }

        return View(model);
    }

    private async Task<ContentItem> GetProductAsync(string contentItemId)
        => string.IsNullOrEmpty(contentItemId) || await _contentManager.GetAsync(contentItemId, VersionOptions.Latest) is not { } item || !item.Has<BookingPart>()
            ? null
            : item;

    private async Task<BookingDatesViewModel> BuildDatesAsync(ContentItem item, BookingDatesViewModel model)
    {
        model.ContentItemId = item.ContentItemId;
        model.Title = item.DisplayText;
        model.Part = item.TryGet<BookingPart>(out var part) ? part : new BookingPart();
        model.Currency = item.TryGet<CrestApps.OrchardCore.Products.Core.Models.ProductPart>(out var product) ? product.Currency : null;
        model.Slots = [.. await _slots.ListAsync(item.ContentItemId, _clock.UtcNow.AddDays(-1), _clock.UtcNow.AddDays(800), HttpContext.RequestAborted)];

        return model;
    }
}
