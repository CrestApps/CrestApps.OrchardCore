using System.Globalization;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Adds an event to a calendar.
/// </summary>
public sealed class CreateCalendarEventTask : CalendarTaskActivity<CreateCalendarEventTask>
{
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCalendarEventTask"/> class.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="workflowService">The calendar workflow service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CreateCalendarEventTask(
        ICalendarService calendarService,
        ICalendarEventManager eventManager,
        ILogger<CreateCalendarEventTask> logger,
        CalendarWorkflowService workflowService,
        IStringLocalizer<CreateCalendarEventTask> stringLocalizer)
        : base(workflowService, stringLocalizer)
    {
        _calendarService = calendarService;
        _eventManager = eventManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Create Calendar Event"];

    /// <summary>
    /// Gets or sets the calendar id field, a Liquid expression.
    /// </summary>
    public string CalendarId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the kind of event field, a Liquid expression.
    /// </summary>
    public string EventType
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the title field, a Liquid expression.
    /// </summary>
    public string Title
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the starts field, a Liquid expression.
    /// </summary>
    public string Start
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the ends field, a Liquid expression.
    /// </summary>
    public string End
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the location field, a Liquid expression.
    /// </summary>
    public string Location
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the description field, a Liquid expression.
    /// </summary>
    public string Description
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the attendees field, a Liquid expression.
    /// </summary>
    public string Attendees
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var calendar = await _calendarService.FindByIdAsync(await Calendars.EvaluateAsync(CalendarId, workflowContext));

        if (calendar is null)
        {
            return Outcome("Failed");
        }

        var calendarEvent = await _eventManager.NewAsync(calendar, await Calendars.EvaluateAsync(EventType, workflowContext));
        var start = await Calendars.EvaluateAsync(Start, workflowContext);
        var end = await Calendars.EvaluateAsync(End, workflowContext);

        calendarEvent.Title = await Calendars.EvaluateAsync(Title, workflowContext);
        calendarEvent.Location = await Calendars.EvaluateAsync(Location, workflowContext);
        calendarEvent.Description = await Calendars.EvaluateAsync(Description, workflowContext);

        foreach (var attendee in CalendarWorkflowService.ParseAttendees(await Calendars.EvaluateAsync(Attendees, workflowContext)))
        {
            calendarEvent.Attendees.Add(attendee);
        }

        if (start is not null && start.Length == 10 && DateTime.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            calendarEvent.AllDay = true;
            calendarEvent.Start = day;
            calendarEvent.End = end is not null && DateTime.TryParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastDay) && lastDay >= day ? lastDay.AddDays(1) : day.AddDays(1);
        }
        else if (CalendarWorkflowService.TryParseTime(start, calendarEvent.TimeZoneId, out var localStart))
        {
            var length = calendarEvent.End - calendarEvent.Start;

            calendarEvent.AllDay = false;
            calendarEvent.Start = localStart;
            calendarEvent.End = CalendarWorkflowService.TryParseTime(end, calendarEvent.TimeZoneId, out var localEnd) ? localEnd : localStart + length;
        }

        var result = await _eventManager.CreateAsync(calendarEvent, new CalendarChangeOptions { Actor = CalendarsConstants.WorkflowActor });

        if (!result.Succeeded)
        {
            _logger.LogWarning("The Create Calendar Event task of workflow '{WorkflowId}' failed: {Errors}", workflowContext.WorkflowId, string.Join(" ", result.Errors.Select(error => error.Message)));

            return Outcome("Failed");
        }

        var output = CalendarWorkflowInput.ToDictionary(result.Value);

        workflowContext.Output[GetOutputName("CalendarEvent")] = output;
        workflowContext.LastResult = output;

        return Outcome("Done");
    }
}
