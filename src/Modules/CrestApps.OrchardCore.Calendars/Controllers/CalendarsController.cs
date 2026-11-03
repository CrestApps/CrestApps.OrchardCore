using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Calendars.Controllers;

/// <summary>
/// The list of calendars, and the pages that create, change and delete them.
/// </summary>
public sealed class CalendarsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICalendarAccessService _accessService;
    private readonly ICalendarService _calendarService;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IDisplayManager<Calendar> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="accessService">The calendar access service.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="displayManager">The calendar display manager.</param>
    /// <param name="updateModelAccessor">The model updater accessor.</param>
    /// <param name="localizerFactory">The localizer factory.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarsController(
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        ICalendarService calendarService,
        ICalendarTypeManager typeManager,
        IDisplayManager<Calendar> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IStringLocalizerFactory localizerFactory,
        INotifier notifier,
        IHtmlLocalizer<CalendarsController> htmlLocalizer,
        IStringLocalizer<CalendarsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _accessService = accessService;
        _calendarService = calendarService;
        _typeManager = typeManager;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _localizerFactory = localizerFactory;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the calendars the viewer can see.
    /// </summary>
    /// <param name="search">The text the name must contain.</param>
    /// <param name="type">The calendar type to show.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("calendars", "CalendarsIndex")]
    public async Task<IActionResult> Index(
        string search,
        string type,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CalendarPermissions.AccessCalendars))
        {
            return Forbid();
        }

        var accessible = (await _accessService.ListAccessibleAsync(User, CalendarAccessLevel.FreeBusy))
            .Where(access => string.IsNullOrWhiteSpace(search) || access.Calendar.Name?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true)
            .Where(access => string.IsNullOrEmpty(type) || string.Equals(access.Calendar.Type, type, StringComparison.OrdinalIgnoreCase))
            .Select(access => access.Calendar)
            .ToList();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var pagerShape = await shapeFactory.PagerAsync(pager, accessible.Count, new Microsoft.AspNetCore.Routing.RouteValueDictionary
        {
            { "search", search },
            { "type", type },
        });

        var model = new CalendarListViewModel
        {
            Search = search,
            Type = type,
            List = new AdminListContext<Calendar>
            {
                Source = accessible.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = pagerShape,
            },
        };

        foreach (var descriptor in _typeManager.ListTypes())
        {
            var displayName = _localizerFactory.Localize(descriptor.DisplayName).Value;

            model.TypeOptions.Add(new SelectListItem(displayName, descriptor.Name, string.Equals(descriptor.Name, type, StringComparison.OrdinalIgnoreCase)));

            if (await _accessService.CanCreateAsync(User, descriptor.Name))
            {
                model.CreatableTypes.Add(new CalendarTypeOption
                {
                    Name = descriptor.Name,
                    DisplayName = displayName,
                    Description = descriptor.Description is null ? null : _localizerFactory.Localize(descriptor.Description).Value,
                    Icon = descriptor.Icon,
                });
            }
        }

        return View(model);
    }

    /// <summary>
    /// Shows the editor for a new calendar.
    /// </summary>
    /// <param name="type">The calendar type.</param>
    /// <returns>The editor.</returns>
    [Admin("calendars/create/{type}", "CalendarsCreate")]
    public async Task<IActionResult> Create(string type)
    {
        if (!await _accessService.CanCreateAsync(User, type))
        {
            return Forbid();
        }

        var calendar = await NewCalendarAsync(type);

        if (calendar is null)
        {
            return NotFound();
        }

        return View(new EditCalendarViewModel
        {
            Type = type,
            Title = GetNewTitle(type),
            Editor = await _displayManager.BuildEditorAsync(calendar, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Creates a calendar.
    /// </summary>
    /// <param name="type">The calendar type.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("calendars/create/{type}", "CalendarsCreate")]
    public async Task<IActionResult> CreatePost(string type)
    {
        if (!await _accessService.CanCreateAsync(User, type))
        {
            return Forbid();
        }

        var calendar = await NewCalendarAsync(type);

        if (calendar is null)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(calendar, _updateModelAccessor.ModelUpdater, isNew: true);

        if (ModelState.IsValid)
        {
            EnsureCreatorCanManage(calendar);

            var result = await _calendarService.CreateAsync(calendar, new CalendarChangeOptions { Actor = CalendarUser.GetId(User) });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The calendar has been created."]);

                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Errors);
        }

        return View(new EditCalendarViewModel
        {
            Type = type,
            Title = GetNewTitle(type),
            Editor = editor,
        });
    }

    /// <summary>
    /// Shows the editor of a calendar.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <returns>The editor.</returns>
    [Admin("calendars/edit/{id}", "CalendarsEdit")]
    public async Task<IActionResult> Edit(string id)
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

        return View(new EditCalendarViewModel
        {
            Id = id,
            Type = calendar.Type,
            Title = calendar.Name,
            Editor = await _displayManager.BuildEditorAsync(calendar, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves changes to a calendar.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("calendars/edit/{id}", "CalendarsEdit")]
    public async Task<IActionResult> EditPost(string id)
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

        var editor = await _displayManager.UpdateEditorAsync(calendar, _updateModelAccessor.ModelUpdater, isNew: false);

        if (ModelState.IsValid)
        {
            var result = await _calendarService.UpdateAsync(calendar, new CalendarChangeOptions { Actor = CalendarUser.GetId(User) });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The calendar has been saved."]);

                return RedirectToAction(nameof(Index));
            }

            AddErrors(result.Errors);
        }

        return View(new EditCalendarViewModel
        {
            Id = id,
            Type = calendar.Type,
            Title = calendar.Name,
            Editor = editor,
        });
    }

    /// <summary>
    /// Deletes a calendar and its events.
    /// </summary>
    /// <param name="id">The calendar's id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("calendars/delete/{id}", "CalendarsDelete")]
    public async Task<IActionResult> Delete(string id)
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

        if (!string.IsNullOrEmpty(calendar.Source))
        {
            await _notifier.WarningAsync(H["The calendar is managed by {0} and can't be deleted here.", calendar.Source]);

            return RedirectToAction(nameof(Index));
        }

        await _calendarService.DeleteAsync(calendar, new CalendarChangeOptions { Actor = CalendarUser.GetId(User) });
        await _notifier.SuccessAsync(H["The calendar has been deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<Calendar> NewCalendarAsync(string type)
    {
        var calendar = await _calendarService.NewAsync(type);

        if (calendar?.OwnerKind == CalendarOwnerKind.User)
        {
            calendar.OwnerId = CalendarUser.GetId(User);
            calendar.Name = User.Identity?.Name;
        }

        return calendar;
    }

    private void EnsureCreatorCanManage(Calendar calendar)
    {
        var userId = CalendarUser.GetId(User);

        if (string.IsNullOrEmpty(userId) || (calendar.OwnerKind == CalendarOwnerKind.User && calendar.OwnerId == userId))
        {
            return;
        }

        if (!calendar.Shares.Any(share => share.PrincipalKind == CalendarPrincipalKind.User && share.PrincipalId == userId && share.Access == CalendarAccessLevel.Manage))
        {
            calendar.Shares.Add(new CalendarShare
            {
                PrincipalKind = CalendarPrincipalKind.User,
                PrincipalId = userId,
                DisplayName = User.Identity?.Name,
                Access = CalendarAccessLevel.Manage,
            });
        }
    }

    private string GetNewTitle(string type)
    {
        var descriptor = _typeManager.FindType(type);

        return descriptor is null ? S["New Calendar"] : S["New {0} Calendar", _localizerFactory.Localize(descriptor.DisplayName).Value];
    }

    private void AddErrors(IEnumerable<CalendarError> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }
    }
}
