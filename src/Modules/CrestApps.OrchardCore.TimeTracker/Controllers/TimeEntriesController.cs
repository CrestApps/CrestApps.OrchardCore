using System.Globalization;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.TimeTracker.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Users;
using YesSql;

namespace CrestApps.OrchardCore.TimeTracker.Controllers;

/// <summary>
/// Everyone's time entries: the list, and the form managers add and change time with.
/// </summary>
public sealed class TimeEntriesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeTrackerService _tracker;
    private readonly TimeStageStore _stages;
    private readonly TimeTrackerTimeZone _timeZone;
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeEntriesController"/> class.
    /// </summary>
    public TimeEntriesController(IAuthorizationService authorizationService, ITimeTrackerService tracker, TimeStageStore stages, TimeTrackerTimeZone timeZone, UserManager<IUser> userManager, ISession session, INotifier notifier, IHtmlLocalizer<TimeEntriesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _tracker = tracker;
        _stages = stages;
        _timeZone = timeZone;
        _userManager = userManager;
        _session = session;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists time entries.
    /// </summary>
    [Admin("time/entries", "TimeTrackerEntries")]
    public async Task<IActionResult> Index(TimeEntriesListViewModel form, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ViewTimeEntries))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();
        var stages = (await _stages.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(stage => stage.ItemId, StringComparer.Ordinal);
        var query = _session.Query<TimeEntry, TimeEntryIndex>(collection: TimeTrackerConstants.CollectionName);

        if (!string.IsNullOrWhiteSpace(form.Q))
        {
            var q = form.Q.Trim();

            query = query.Where(index => index.UserName.Contains(q));
        }

        if (!string.IsNullOrEmpty(form.StageId))
        {
            query = query.Where(index => index.StageId == form.StageId);
        }

        if (DateTime.TryParseExact(form.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            var from = TimeTrackerTimeZone.ToUtc(day, zone);
            var to = TimeTrackerTimeZone.ToUtc(day.AddDays(1), zone);

            query = query.Where(index => index.StartUtc < to && index.EndUtc > from);
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var count = await query.CountAsync(HttpContext.RequestAborted);
        var page = await query.OrderByDescending(index => index.StartUtc).Skip(pager.GetStartIndex()).Take(pager.PageSize).ListAsync(HttpContext.RequestAborted);
        var canManage = await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeEntries);

        form.CanManage = canManage;
        form.StageOptions = [.. stages.Values.OrderBy(stage => stage.Position).ThenBy(stage => stage.Name, StringComparer.CurrentCultureIgnoreCase).Select(stage => new SelectListItem(stage.Name, stage.ItemId, stage.ItemId == form.StageId))];
        var rows = new List<TimeEntryRow>();

        foreach (var entry in page)
        {
            rows.Add(new TimeEntryRow
            {
                Entry = entry,
                Stage = stages.GetValueOrDefault(entry.StageId ?? string.Empty),
                TimeZoneId = zone,
                CanManage = canManage,
                KeptBy = (await KeptByAsync(entry))?.Name,
            });
        }

        form.List = new AdminListContext<TimeEntryRow>
        {
            Source = rows,
            Pager = await shapeFactory.PagerAsync(pager, count),
        };

        return View(form);
    }

    /// <summary>
    /// Shows the form that adds or changes an entry.
    /// </summary>
    [Admin("time/entries/edit/{id?}", "TimeTrackerEntriesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeEntries))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();
        var model = new TimeEntryEditViewModel { TimeZoneId = zone };

        if (!string.IsNullOrEmpty(id))
        {
            var entry = await _tracker.FindAsync(id, HttpContext.RequestAborted);

            if (entry is null)
            {
                return NotFound();
            }

            model.Id = entry.ItemId;
            model.UserId = entry.UserId;
            model.UserName = entry.UserName;
            model.StageId = entry.StageId;
            model.Start = TimeTrackerTimeZone.ToLocal(entry.StartUtc, zone);
            model.End = entry.EndUtc is { } end ? TimeTrackerTimeZone.ToLocal(end, zone) : null;
            model.Note = entry.Note;
            model.IsOpen = entry.IsOpen;
            model.Changes = [.. entry.Changes];
            model.ExternalClock = await KeptByAsync(entry);
        }

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Adds or changes an entry.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("time/entries/edit/{id?}", "TimeTrackerEntriesEdit")]
    public async Task<IActionResult> EditPost(TimeEntryEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeEntries))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();

        form.TimeZoneId = zone;

        if (form.Start is null)
        {
            ModelState.AddModelError(nameof(form.Start), H["Enter when it starts."].Value);
        }

        if (string.IsNullOrWhiteSpace(form.Reason))
        {
            ModelState.AddModelError(nameof(form.Reason), H["Say why the time is added or changed."].Value);
        }

        TimeTrackerResult result = null;

        if (ModelState.IsValid)
        {
            var start = TimeTrackerTimeZone.ToUtc(form.Start.Value, zone);
            DateTime? end = form.End is { } local ? TimeTrackerTimeZone.ToUtc(local, zone) : null;

            if (string.IsNullOrEmpty(form.Id))
            {
                var user = string.IsNullOrEmpty(form.UserId) ? null : await _userManager.FindByIdAsync(form.UserId);

                result = user is null
                    ? TimeTrackerResult.Fail(H["Choose whose time it is."].Value)
                    : await _tracker.AddAsync(new TimeEntry { UserId = form.UserId, UserName = (HttpContext.RequestServices.GetService<IDisplayNameProvider>() is { } names ? await names.GetAsync(user, HttpContext.RequestAborted) : null) ?? user.UserName, StageId = form.StageId, StartUtc = start, EndUtc = end, Note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim(), Source = TimeTrackerConstants.Sources.Manager }, User.Identity?.Name, form.Reason, HttpContext.RequestAborted);
            }
            else
            {
                result = await _tracker.UpdateAsync(form.Id, form.StageId, start, end, form.Note, User.Identity?.Name, form.Reason, HttpContext.RequestAborted);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        if (!ModelState.IsValid)
        {
            if (!string.IsNullOrEmpty(form.Id) && await _tracker.FindAsync(form.Id, HttpContext.RequestAborted) is { } entry)
            {
                form.UserName = entry.UserName;
                form.IsOpen = entry.IsOpen;
                form.Changes = [.. entry.Changes];
            }

            await PopulateAsync(form);

            return View(form);
        }

        await _notifier.SuccessAsync(H["The time was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Removes an entry.
    /// </summary>
    [HttpPost]
    [Admin("time/entries/delete/{id}", "TimeTrackerEntriesDelete")]
    public async Task<IActionResult> Delete(string id, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeEntries))
        {
            return Forbid();
        }

        var result = await _tracker.DeleteAsync(id, User.Identity?.Name, reason, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The time was removed."]);

            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task PopulateAsync(TimeEntryEditViewModel model)
    {
        model.StageOptions = [.. (await _stages.GetAllAsync(HttpContext.RequestAborted))
            .Where(stage => stage.Enabled || stage.ItemId == model.StageId)
            .OrderBy(stage => stage.Position)
            .ThenBy(stage => stage.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(stage => new SelectListItem(stage.Name, stage.ItemId, stage.ItemId == model.StageId))];
    }

    // The clock that keeps an entry, when it is kept by another one.
    private async Task<ExternalTimeClockInfo> KeptByAsync(TimeEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ManagedBy))
        {
            return null;
        }

        foreach (var clock in HttpContext.RequestServices.GetServices<IExternalTimeClock>())
        {
            if (await clock.FindByIdAsync(entry.ManagedBy, HttpContext.RequestAborted) is { } found)
            {
                return found;
            }
        }

        return new ExternalTimeClockInfo { Id = entry.ManagedBy, Name = entry.ManagedBy };
    }
}
