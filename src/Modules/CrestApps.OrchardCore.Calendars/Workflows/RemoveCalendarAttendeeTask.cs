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
/// Removes someone from an event.
/// </summary>
public sealed class RemoveCalendarAttendeeTask : CalendarTaskActivity<RemoveCalendarAttendeeTask>
{
    private readonly ICalendarEventManager _eventManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveCalendarAttendeeTask"/> class.
    /// </summary>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="workflowService">The calendar workflow service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RemoveCalendarAttendeeTask(
        ICalendarEventManager eventManager,
        CalendarWorkflowService workflowService,
        IStringLocalizer<RemoveCalendarAttendeeTask> stringLocalizer)
        : base(workflowService, stringLocalizer)
    {
        _eventManager = eventManager;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Remove Calendar Attendee"];

    /// <summary>
    /// Gets or sets the event id field, a Liquid expression.
    /// </summary>
    public string EventId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the attendee field, a Liquid expression.
    /// </summary>
    public string Attendees
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

        var attendee = CalendarWorkflowService.ParseAttendees(await Calendars.EvaluateAsync(Attendees, workflowContext)).FirstOrDefault();

        if (attendee is null)
        {
            return Outcome("Failed");
        }

        var result = await _eventManager.RemoveAttendeeAsync(calendarEvent, attendee.GetKey(), new CalendarChangeOptions { Actor = CalendarsConstants.WorkflowActor });

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }
}
