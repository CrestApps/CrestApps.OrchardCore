using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// The editor of a calendar workflow event: the calendars, calendar types and event types that start the workflow.
/// </summary>
/// <typeparam name="TActivity">The event type.</typeparam>
public sealed class CalendarEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, CalendarEventActivityViewModel>
    where TActivity : CalendarEventActivity
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IStringLocalizerFactory _localizerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="localizerFactory">The localizer factory.</param>
    public CalendarEventActivityDisplayDriver(
        ICalendarService calendarService,
        ICalendarTypeManager typeManager,
        IStringLocalizerFactory localizerFactory)
    {
        _calendarService = calendarService;
        _typeManager = typeManager;
        _localizerFactory = localizerFactory;
    }

    /// <inheritdoc />
    protected override async ValueTask EditActivityAsync(TActivity activity, CalendarEventActivityViewModel model)
    {
        model.CalendarIds = activity.CalendarIds.ToArray();
        model.CalendarTypes = activity.CalendarTypes.ToArray();
        model.EventTypes = activity.EventTypes;
        model.IgnoreWorkflowChanges = activity.IgnoreWorkflowChanges;
        model.HasMinutesBefore = activity is CalendarEventStartingSoonEvent;
        model.MinutesBefore = activity is CalendarEventStartingSoonEvent startingSoon ? startingSoon.MinutesBefore : 15;
        model.CalendarOptions = (await _calendarService.ListAsync(new CalendarListQuery { Limit = 500 }))
            .Select(calendar => new SelectListItem(calendar.Name, calendar.ItemId, model.CalendarIds.Contains(calendar.ItemId)))
            .ToList();
        model.TypeOptions = _typeManager.ListTypes()
            .Select(type => new SelectListItem(_localizerFactory.Localize(type.DisplayName).Value, type.Name, model.CalendarTypes.Contains(type.Name, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <inheritdoc />
    protected override void UpdateActivity(CalendarEventActivityViewModel model, TActivity activity)
    {
        activity.CalendarIds = (model.CalendarIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        activity.CalendarTypes = (model.CalendarTypes ?? []).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        activity.EventTypes = string.IsNullOrWhiteSpace(model.EventTypes) ? null : model.EventTypes.Trim();
        activity.IgnoreWorkflowChanges = model.IgnoreWorkflowChanges;

        if (activity is CalendarEventStartingSoonEvent startingSoon)
        {
            startingSoon.MinutesBefore = Math.Clamp(model.MinutesBefore, 0, 43200);
        }
    }
}
