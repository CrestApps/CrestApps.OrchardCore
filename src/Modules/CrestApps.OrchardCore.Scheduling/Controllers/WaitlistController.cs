using System.Globalization;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using CrestApps.OrchardCore.Calendars.Core;

namespace CrestApps.OrchardCore.Scheduling.Controllers;

/// <summary>
/// The waitlist: customers waiting for a time to free up.
/// </summary>
public sealed class WaitlistController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly WaitlistStore _waitlistStore;
    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingPageService _pageService;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="WaitlistController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="pageService">The page builder.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public WaitlistController(
        IAuthorizationService authorizationService,
        WaitlistStore waitlistStore,
        AppointmentTypeStore typeStore,
        SchedulingPageService pageService,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<WaitlistController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _waitlistStore = waitlistStore;
        _typeStore = typeStore;
        _pageService = pageService;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the waitlist.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("scheduling/waitlist", "SchedulingWaitlist")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageAppointments))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, entries) = await _waitlistStore.PageAsync(pager.GetStartIndex(), pager.PageSize);

        ViewData["Types"] = (await _typeStore.ListAsync()).Select(type => new SelectListItem(type.Name, type.ItemId)).ToList();

        return View(new SchedulingListViewModel
        {
            List = new AdminListContext<WaitlistEntry>
            {
                Source = entries,
                Pager = await shapeFactory.PagerAsync(pager, count),
            },
        });
    }

    /// <summary>
    /// Adds a customer to the waitlist.
    /// </summary>
    /// <param name="appointmentTypeId">The appointment type.</param>
    /// <param name="from">The first day the customer can come, as yyyy-MM-dd.</param>
    /// <param name="to">The last day the customer can come, as yyyy-MM-dd.</param>
    /// <param name="customerName">The customer's name.</param>
    /// <param name="customerEmail">The customer's email.</param>
    /// <param name="customerPhone">The customer's phone number.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("scheduling/waitlist/add", "SchedulingWaitlistAdd")]
    public async Task<IActionResult> Add(string appointmentTypeId, string from, string to, string customerName, string customerEmail, string customerPhone)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageAppointments))
        {
            return Forbid();
        }

        var timeZoneId = await _pageService.GetTimeZoneIdAsync();

        if (await _typeStore.FindByIdAsync(appointmentTypeId ?? string.Empty) is null ||
            string.IsNullOrWhiteSpace(customerName) ||
            !DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) || last < first)
        {
            await _notifier.ErrorAsync(H["Choose the appointment type, the days and the customer's name."]);

            return RedirectToAction(nameof(Index));
        }

        await _waitlistStore.CreateAsync(new WaitlistEntry
        {
            AppointmentTypeId = appointmentTypeId,
            FromUtc = CalendarTime.ToUtc(first, timeZoneId),
            ToUtc = CalendarTime.ToUtc(last.AddDays(1), timeZoneId),
            CustomerName = customerName.Trim(),
            CustomerEmail = customerEmail?.Trim(),
            CustomerPhone = customerPhone?.Trim(),
            CreatedUtc = _clock.UtcNow,
        });

        await _notifier.SuccessAsync(H["The customer has been added to the waitlist."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Removes an entry from the waitlist.
    /// </summary>
    /// <param name="id">The entry's id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("scheduling/waitlist/remove/{id}", "SchedulingWaitlistRemove")]
    public async Task<IActionResult> Remove(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageAppointments))
        {
            return Forbid();
        }

        var entry = await _waitlistStore.FindByIdAsync(id);

        if (entry is not null)
        {
            entry.Status = WaitlistStatus.Removed;
            await _waitlistStore.UpdateAsync(entry);
        }

        return RedirectToAction(nameof(Index));
    }
}
