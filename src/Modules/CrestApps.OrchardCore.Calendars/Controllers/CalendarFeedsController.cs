using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Controllers;

/// <summary>
/// The private subscription address of a calendar, and .ics export and import.
/// </summary>
[Feature(CalendarsConstants.Features.Feeds)]
public sealed class CalendarFeedsController : Controller
{
    private const long MaxImportBytes = 10 * 1024 * 1024;
    private const int PageSize = 500;

    private readonly ICalendarService _calendarService;
    private readonly ICalendarAccessService _accessService;
    private readonly ICalendarEventStore _eventStore;
    private readonly ICalendarImporter _importer;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarFeedsController"/> class.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="accessService">The calendar access service.</param>
    /// <param name="eventStore">The event store.</param>
    /// <param name="importer">The iCalendar importer.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarFeedsController(
        ICalendarService calendarService,
        ICalendarAccessService accessService,
        ICalendarEventStore eventStore,
        ICalendarImporter importer,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<CalendarFeedsController> htmlLocalizer,
        IStringLocalizer<CalendarFeedsController> stringLocalizer)
    {
        _calendarService = calendarService;
        _accessService = accessService;
        _eventStore = eventStore;
        _importer = importer;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows a calendar's subscription address.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <returns>The page.</returns>
    [Admin("calendars/{id}/feed", "CalendarsFeedAddress")]
    public async Task<IActionResult> Feed(string id)
    {
        var calendar = await _calendarService.FindByIdAsync(id);

        if (calendar is null)
        {
            return NotFound();
        }

        if (await _accessService.GetAccessLevelAsync(User, calendar) < CalendarAccessLevel.Manage)
        {
            return Forbid();
        }

        return View(new CalendarFeedViewModel
        {
            Id = calendar.ItemId,
            Name = calendar.Name,
            FeedUrl = string.IsNullOrEmpty(calendar.FeedToken) ? null : Url.RouteUrl("CalendarsFeed", new { token = calendar.FeedToken }, Request.Scheme),
        });
    }

    /// <summary>
    /// Creates, replaces or removes a calendar's subscription address.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <param name="remove">Whether to remove the address.</param>
    /// <returns>A redirect to the address page.</returns>
    [HttpPost]
    [ActionName(nameof(Feed))]
    [Admin("calendars/{id}/feed", "CalendarsFeedAddress")]
    public async Task<IActionResult> FeedPost(string id, bool remove)
    {
        var calendar = await _calendarService.FindByIdAsync(id);

        if (calendar is null)
        {
            return NotFound();
        }

        if (await _accessService.GetAccessLevelAsync(User, calendar) < CalendarAccessLevel.Manage)
        {
            return Forbid();
        }

        calendar.FeedToken = remove ? null : Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        await _calendarService.UpdateAsync(calendar, new CalendarChangeOptions { Actor = CalendarUser.GetId(User) });
        await _notifier.SuccessAsync(remove ? H["The subscription address has been removed."] : H["A new subscription address has been made. The old one no longer works."]);

        return RedirectToAction(nameof(Feed), new { id });
    }

    /// <summary>
    /// Downloads a calendar as an .ics file.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <returns>The file.</returns>
    [Admin("calendars/{id}/export", "CalendarsExport")]
    public async Task<IActionResult> Export(string id)
    {
        var calendar = await _calendarService.FindByIdAsync(id);

        if (calendar is null)
        {
            return NotFound();
        }

        if (await _accessService.GetAccessLevelAsync(User, calendar) < CalendarAccessLevel.Read)
        {
            return Forbid();
        }

        var events = new List<CalendarEvent>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _eventStore.ListByCalendarAsync(calendar.ItemId, skip, PageSize);

            events.AddRange(page);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var text = ICalendarWriter.Write(calendar.Name, events, _clock.UtcNow);
        var fileName = string.Concat((calendar.Name ?? "calendar").Select(character => char.IsLetterOrDigit(character) ? character : '-')) + ".ics";

        return File(Encoding.UTF8.GetBytes(text), "text/calendar", fileName);
    }

    /// <summary>
    /// Shows the page that imports an .ics file.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <returns>The page.</returns>
    [Admin("calendars/{id}/import", "CalendarsImport")]
    public async Task<IActionResult> Import(string id)
    {
        var calendar = await _calendarService.FindByIdAsync(id);

        if (calendar is null)
        {
            return NotFound();
        }

        if (await _accessService.GetAccessLevelAsync(User, calendar) < CalendarAccessLevel.Write)
        {
            return Forbid();
        }

        return View(new CalendarFeedViewModel { Id = calendar.ItemId, Name = calendar.Name });
    }

    /// <summary>
    /// Imports an .ics file into a calendar.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <param name="file">The file.</param>
    /// <returns>A redirect to the calendar.</returns>
    [HttpPost]
    [ActionName(nameof(Import))]
    [Admin("calendars/{id}/import", "CalendarsImport")]
    public async Task<IActionResult> ImportPost(string id, IFormFile file)
    {
        var calendar = await _calendarService.FindByIdAsync(id);

        if (calendar is null)
        {
            return NotFound();
        }

        if (await _accessService.GetAccessLevelAsync(User, calendar) < CalendarAccessLevel.Write)
        {
            return Forbid();
        }

        if (file is null || file.Length == 0 || file.Length > MaxImportBytes)
        {
            await _notifier.ErrorAsync(H["Choose an .ics file of up to 10 MB."]);

            return RedirectToAction(nameof(Import), new { id });
        }

        string text;

        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
        {
            text = await reader.ReadToEndAsync();
        }

        var events = ICalendarReader.Read(text, calendar.TimeZoneId ?? "UTC");
        var result = await _importer.ImportAsync(calendar, events, new ICalendarImportOptions { Actor = CalendarUser.GetId(User) });

        await _notifier.SuccessAsync(H["{0} events added, {1} changed, {2} unchanged.", result.Created, result.Updated, result.Unchanged]);

        foreach (var error in result.Errors.Take(10))
        {
            await _notifier.WarningAsync(H["Not imported: {0}", error]);
        }

        return RedirectToAction("Index", "Calendar", new { area = CalendarsConstants.Features.Calendars, calendars = calendar.ItemId });
    }
}
