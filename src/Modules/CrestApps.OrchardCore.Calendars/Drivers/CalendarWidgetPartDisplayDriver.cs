using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// Shows calendars on the admin dashboard, and lets the widget choose which calendars (the viewer's own, every calendar
/// of a type, or named calendars) and the view it opens on. What a viewer sees is limited to the calendars they may open.
/// </summary>
public sealed class CalendarWidgetPartDisplayDriver : ContentPartDisplayDriver<CalendarWidgetPart>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuthorizationService _authorizationService;
    private readonly ICalendarAccessService _accessService;
    private readonly ICalendarService _calendarService;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarWidgetPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="accessService">The calendar access service.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="localizerFactory">The localizer factory.</param>
    /// <param name="httpContextAccessor">The accessor of the current request.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="localClock">The clock of the user's time zone.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarWidgetPartDisplayDriver(
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        ICalendarService calendarService,
        ICalendarTypeManager typeManager,
        IStringLocalizerFactory localizerFactory,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        ILocalClock localClock,
        IStringLocalizer<CalendarWidgetPartDisplayDriver> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _accessService = accessService;
        _calendarService = calendarService;
        _typeManager = typeManager;
        _localizerFactory = localizerFactory;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(CalendarWidgetPart part, BuildPartDisplayContext context)
    {
        return Initialize<CalendarWidgetViewModel>(GetDisplayShapeType(context), async model =>
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext.User;

            model.CanView = await _authorizationService.AuthorizeAsync(user, CalendarPermissions.AccessCalendars);
            model.IsConfigured = part.Source switch
            {
                CalendarWidgetSource.Type => !string.IsNullOrEmpty(part.CalendarType),
                CalendarWidgetSource.Calendars => part.CalendarIds?.Length > 0,
                _ => true,
            };

            if (!model.CanView || !model.IsConfigured)
            {
                return;
            }

            foreach (var access in await GetShownCalendarsAsync(part, user, httpContext.RequestAborted))
            {
                var type = _typeManager.FindType(access.Calendar.Type);

                model.Calendars.Add(new CalendarViewCalendar
                {
                    Id = access.Calendar.ItemId,
                    Name = access.Calendar.Name,
                    Color = access.Calendar.Color ?? type?.DefaultColor ?? "#1971c2",
                    TypeName = type is null ? access.Calendar.Type : _localizerFactory.Localize(type.DisplayName).Value,
                    TypeIcon = type?.Icon ?? "fa-solid fa-calendar",
                    Selected = true,
                });
            }

            var timeZone = await _localClock.GetLocalTimeZoneAsync();
            var today = await _localClock.GetLocalNowAsync();

            model.PageView = part.View switch
            {
                CalendarWidgetView.Day => "day",
                CalendarWidgetView.Week => "week",
                CalendarWidgetView.Month => "month",
                _ => "agenda",
            };
            model.ConfigurationJson = JsonSerializer.Serialize(new
            {
                occurrencesUrl = _linkGenerator.GetPathByName(httpContext, "CalendarsOccurrences"),
                calendars = model.Calendars.Select(calendar => calendar.Id),
                initialDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                timeZone = timeZone.TimeZoneId,
                initialView = part.View switch
                {
                    CalendarWidgetView.Day => "timeGridDay",
                    CalendarWidgetView.Week => "timeGridWeek",
                    CalendarWidgetView.Month => "dayGridMonth",
                    _ => "listWeek",
                },
                locale = CultureInfo.CurrentUICulture.Name,
            }, _jsonOptions);
        }).Location("DetailAdmin", "Content:5").Location("Detail", "Content:5");
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(CalendarWidgetPart part, BuildPartEditorContext context)
    {
        return Initialize<CalendarWidgetEditViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Source = part.Source;
            model.CalendarType = part.CalendarType;
            model.CalendarIds = part.CalendarIds ?? [];
            model.View = part.View;
            model.Views =
            [
                new SelectListItem(S["Agenda (the week's events)"], nameof(CalendarWidgetView.Agenda)),
                new SelectListItem(S["Day"], nameof(CalendarWidgetView.Day)),
                new SelectListItem(S["Week"], nameof(CalendarWidgetView.Week)),
                new SelectListItem(S["Month"], nameof(CalendarWidgetView.Month)),
            ];
            model.Types = _typeManager.ListTypes()
                .Select(type => new SelectListItem(_localizerFactory.Localize(type.DisplayName).Value, type.Name))
                .ToList();

            var selected = model.CalendarIds.ToHashSet(StringComparer.Ordinal);

            foreach (var access in await ListChoosableAsync())
            {
                var type = _typeManager.FindType(access.Calendar.Type);
                var typeName = type is null ? access.Calendar.Type : _localizerFactory.Localize(type.DisplayName).Value;

                model.AvailableCalendars.Add(new SelectListItem($"{access.Calendar.Name} ({typeName})", access.Calendar.ItemId, selected.Contains(access.Calendar.ItemId)));
            }
        });
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(CalendarWidgetPart part, UpdatePartEditorContext context)
    {
        var model = new CalendarWidgetEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Source, m => m.CalendarType, m => m.CalendarIds, m => m.View);

        if (!Enum.IsDefined(model.Source))
        {
            model.Source = CalendarWidgetSource.MyCalendars;
        }

        if (!Enum.IsDefined(model.View))
        {
            model.View = CalendarWidgetView.Agenda;
        }

        part.Source = model.Source;
        part.View = model.View;
        part.CalendarType = null;
        part.CalendarIds = [];

        if (model.Source == CalendarWidgetSource.Type)
        {
            if (string.IsNullOrEmpty(model.CalendarType) || _typeManager.FindType(model.CalendarType) is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.CalendarType), S["Choose the calendar type whose calendars the widget shows."]);
            }

            part.CalendarType = model.CalendarType;
        }
        else if (model.Source == CalendarWidgetSource.Calendars)
        {
            // A widget can only be pointed at calendars its editor may open; each viewer then sees the ones they may open.
            var choosable = (await ListChoosableAsync()).Select(access => access.Calendar.ItemId).ToHashSet(StringComparer.Ordinal);
            var calendarIds = (model.CalendarIds ?? []).Where(choosable.Contains).Distinct(StringComparer.Ordinal).ToArray();

            if (calendarIds.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.CalendarIds), S["Choose at least one calendar for the widget to show."]);
            }

            part.CalendarIds = calendarIds;
        }

        return await EditAsync(part, context);
    }

    // The calendars a viewer sees in the widget: of the ones they may at least see as free or busy, those the widget asks for.
    private async Task<IReadOnlyList<CalendarAccess>> GetShownCalendarsAsync(CalendarWidgetPart part, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        switch (part.Source)
        {
            case CalendarWidgetSource.Type:
                return (await _accessService.ListAccessibleAsync(user, CalendarAccessLevel.FreeBusy, part.CalendarType, cancellationToken))
                    .Where(access => access.Calendar.Enabled)
                    .ToList();

            case CalendarWidgetSource.Calendars:
                var named = part.CalendarIds.ToHashSet(StringComparer.Ordinal);

                return (await _accessService.ListAccessibleAsync(user, CalendarAccessLevel.FreeBusy, cancellationToken: cancellationToken))
                    .Where(access => access.Calendar.Enabled && named.Contains(access.Calendar.ItemId))
                    .ToList();

            default:
                // The calendars the calendar page ticks first: the viewer's personal calendar and the ones they may add events to.
                var userId = CalendarUser.GetId(user);
                Calendar personal = null;

                if (!string.IsNullOrEmpty(userId) && _typeManager.FindType(CalendarsConstants.Types.Personal) is not null)
                {
                    personal = await _calendarService.GetOrCreatePersonalCalendarAsync(userId, user.Identity?.Name, cancellationToken);
                }

                return (await _accessService.ListAccessibleAsync(user, CalendarAccessLevel.FreeBusy, cancellationToken: cancellationToken))
                    .Where(access => access.Calendar.Enabled && (access.Calendar.ItemId == personal?.ItemId || access.Level >= CalendarAccessLevel.Write))
                    .ToList();
        }
    }

    private async Task<IReadOnlyList<CalendarAccess>> ListChoosableAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return (await _accessService.ListAccessibleAsync(httpContext.User, CalendarAccessLevel.FreeBusy, cancellationToken: httpContext.RequestAborted))
            .Where(access => access.Calendar.Enabled)
            .OrderBy(access => access.Calendar.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
