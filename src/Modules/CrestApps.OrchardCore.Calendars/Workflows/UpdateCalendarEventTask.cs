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
/// Changes an event's title, time, place or description. Empty fields are left as they are.
/// </summary>
public sealed class UpdateCalendarEventTask : CalendarTaskActivity<UpdateCalendarEventTask>
{
    private readonly ICalendarEventManager _eventManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCalendarEventTask"/> class.
    /// </summary>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="workflowService">The calendar workflow service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UpdateCalendarEventTask(
        ICalendarEventManager eventManager,
        ILogger<UpdateCalendarEventTask> logger,
        CalendarWorkflowService workflowService,
        IStringLocalizer<UpdateCalendarEventTask> stringLocalizer)
        : base(workflowService, stringLocalizer)
    {
        _eventManager = eventManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Update Calendar Event"];

    /// <summary>
    /// Gets or sets the event id field, a Liquid expression.
    /// </summary>
    public string EventId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the occurrence field, a Liquid expression.
    /// </summary>
    public string OccurrenceStart
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

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["NotFound"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var calendarEvent = await _eventManager.FindByIdAsync(await Calendars.EvaluateAsync(EventId, workflowContext));

        if (calendarEvent is null)
        {
            return Outcome("NotFound");
        }

        var title = await Calendars.EvaluateAsync(Title, workflowContext);
        var start = await Calendars.EvaluateAsync(Start, workflowContext);
        var end = await Calendars.EvaluateAsync(End, workflowContext);
        var location = await Calendars.EvaluateAsync(Location, workflowContext);
        var description = await Calendars.EvaluateAsync(Description, workflowContext);
        var occurrence = await Calendars.EvaluateAsync(OccurrenceStart, workflowContext);

        void Change(CalendarEvent target)
        {
            target.Title = title ?? target.Title;
            target.Location = location ?? target.Location;
            target.Description = description ?? target.Description;

            if (CalendarWorkflowService.TryParseTime(start, target.TimeZoneId, out var localStart))
            {
                var length = target.End - target.Start;

                target.Start = localStart;
                target.End = CalendarWorkflowService.TryParseTime(end, target.TimeZoneId, out var localEnd) ? localEnd : localStart + length;
            }
            else if (CalendarWorkflowService.TryParseTime(end, target.TimeZoneId, out var onlyEnd))
            {
                target.End = onlyEnd;
            }
        }

        var options = new CalendarChangeOptions { Actor = CalendarsConstants.WorkflowActor };
        var result = DateTimeOffset.TryParse(occurrence, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var original)
            ? await _eventManager.UpdateOccurrenceAsync(calendarEvent, original.UtcDateTime, CalendarEditScope.ThisOccurrence, Change, options)
            : await _eventManager.UpdateAsync(calendarEvent, Change, options);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The Update Calendar Event task of workflow '{WorkflowId}' failed: {Errors}", workflowContext.WorkflowId, string.Join(" ", result.Errors.Select(error => error.Message)));

            return Outcome("Failed");
        }

        var output = CalendarWorkflowInput.ToDictionary(result.Value);

        workflowContext.Output[GetOutputName("CalendarEvent")] = output;
        workflowContext.LastResult = output;

        return Outcome("Done");
    }
}
