using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Scheduling.Controllers;

/// <summary>
/// The list of scheduling resources and their editor.
/// </summary>
public sealed class SchedulingResourcesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly SchedulingResourceStore _resourceStore;
    private readonly SchedulingResourceCalendars _resourceCalendars;
    private readonly ICalendarService _calendarService;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingResourcesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="resourceCalendars">The resource calendar service.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SchedulingResourcesController(
        IAuthorizationService authorizationService,
        SchedulingResourceStore resourceStore,
        SchedulingResourceCalendars resourceCalendars,
        ICalendarService calendarService,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<SchedulingResourcesController> htmlLocalizer,
        IStringLocalizer<SchedulingResourcesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _resourceStore = resourceStore;
        _resourceCalendars = resourceCalendars;
        _calendarService = calendarService;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the resources.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("scheduling/resources", "SchedulingResources")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var resources = await _resourceStore.ListAsync();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new SchedulingListViewModel
        {
            List = new AdminListContext<SchedulingResource>
            {
                Source = resources.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, resources.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing resource.
    /// </summary>
    /// <param name="id">The resource's id, or empty for a new one.</param>
    /// <param name="siteService">The site service.</param>
    /// <returns>The editor.</returns>
    [Admin("scheduling/resources/edit/{id?}", "SchedulingResourcesEdit")]
    public async Task<IActionResult> Edit(string id, [FromServices] ISiteService siteService)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var resource = string.IsNullOrEmpty(id) ? NewResource() : await _resourceStore.FindByIdAsync(id);

        if (resource is null)
        {
            return NotFound();
        }

        var model = new SchedulingResourceViewModel
        {
            Id = resource.ItemId,
            Name = resource.Name,
            Kind = resource.Kind.ToString(),
            UserId = resource.UserId,
            CalendarId = resource.CalendarId,
            // A new resource starts in the site's time zone rather than the first one in the list.
            TimeZoneId = resource.TimeZoneId ?? (await siteService.GetSiteSettingsAsync()).TimeZoneId,
            AvailabilityMode = resource.AvailabilityMode.ToString(),
            ShiftCalendarId = resource.ShiftCalendarId,
            WorkingHours = SchedulingFormat.WriteHours(resource.WorkingHours),
            DateOverrides = SchedulingFormat.WriteOverrides(resource.DateOverrides),
            Enabled = resource.Enabled,
        };

        await FillAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a resource.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("scheduling/resources/edit/{id?}", "SchedulingResourcesEdit")]
    public async Task<IActionResult> EditPost(SchedulingResourceViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var resource = string.IsNullOrEmpty(model.Id) ? NewResource() : await _resourceStore.FindByIdAsync(model.Id);

        if (resource is null)
        {
            return NotFound();
        }

        var errors = new List<string>();
        var hours = SchedulingFormat.ReadHours(model.WorkingHours, errors);
        var overrides = SchedulingFormat.ReadOverrides(model.DateOverrides, errors);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["The name is required."]);
        }

        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(model.WorkingHours), S["This line can't be read: {0}", error]);
        }

        if (!string.IsNullOrWhiteSpace(model.TimeZoneId) && !CalendarTime.IsValidZone(model.TimeZoneId))
        {
            ModelState.AddModelError(nameof(model.TimeZoneId), S["'{0}' is not a time zone.", model.TimeZoneId]);
        }

        if (!ModelState.IsValid)
        {
            await FillAsync(model);

            return View(model);
        }

        resource.Name = model.Name.Trim();
        resource.Kind = Enum.TryParse<SchedulingResourceKind>(model.Kind, out var kind) ? kind : SchedulingResourceKind.Staff;
        resource.UserId = resource.Kind == SchedulingResourceKind.Staff && !string.IsNullOrWhiteSpace(model.UserId) ? model.UserId.Trim() : null;
        resource.CalendarId = string.IsNullOrWhiteSpace(model.CalendarId) ? null : model.CalendarId;
        resource.TimeZoneId = CalendarTime.NormalizeZoneId(model.TimeZoneId) ?? resource.TimeZoneId ?? "UTC";
        resource.AvailabilityMode = Enum.TryParse<SchedulingAvailabilityMode>(model.AvailabilityMode, out var mode) ? mode : SchedulingAvailabilityMode.WorkingHours;
        resource.ShiftCalendarId = string.IsNullOrWhiteSpace(model.ShiftCalendarId) ? null : model.ShiftCalendarId;
        resource.WorkingHours = hours;
        resource.DateOverrides = overrides;
        resource.Enabled = model.Enabled;

        if (string.IsNullOrEmpty(resource.ItemId))
        {
            resource.ItemId = CrestApps.Core.UniqueId.GenerateId();
            await _resourceCalendars.EnsureCalendarAsync(resource);
            await _resourceStore.CreateAsync(resource);
        }
        else
        {
            await _resourceCalendars.EnsureCalendarAsync(resource);
            await _resourceStore.UpdateAsync(resource);
        }

        await _notifier.SuccessAsync(H["The resource has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a resource.
    /// </summary>
    /// <param name="id">The resource's id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("scheduling/resources/delete/{id}", "SchedulingResourcesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageScheduling))
        {
            return Forbid();
        }

        var resource = await _resourceStore.FindByIdAsync(id);

        if (resource is not null)
        {
            await _resourceStore.DeleteAsync(resource);
            await _notifier.SuccessAsync(H["The resource has been deleted. Its calendar and appointments are kept."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private static SchedulingResource NewResource()
    {
        var resource = new SchedulingResource();

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            resource.WorkingHours.Add(new SchedulingWorkingHours { Day = day, Start = "09:00", End = "17:00" });
        }

        return resource;
    }

    private async Task FillAsync(SchedulingResourceViewModel model)
    {
        var calendars = await _calendarService.ListAsync(new CalendarListQuery { Enabled = true, Limit = 500 });

        model.CalendarOptions = calendars.Select(calendar => new SelectListItem($"{calendar.Name} ({calendar.Type})", calendar.ItemId, calendar.ItemId == model.CalendarId)).ToList();
        model.KindOptions =
        [
            new SelectListItem(S["Staff member"], nameof(SchedulingResourceKind.Staff)),
            new SelectListItem(S["Room"], nameof(SchedulingResourceKind.Room)),
            new SelectListItem(S["Equipment"], nameof(SchedulingResourceKind.Equipment)),
            new SelectListItem(S["Other"], nameof(SchedulingResourceKind.Other)),
        ];
        model.TimeZoneOptions = _clock.GetTimeZones().Select(zone => new SelectListItem(zone.TimeZoneId, zone.TimeZoneId, zone.TimeZoneId == model.TimeZoneId)).ToList();
        model.InitialUserJson = string.IsNullOrEmpty(model.UserId) ? "[]" : JsonSerializer.Serialize(new[] { new { value = model.UserId, text = model.UserId } });
    }
}
