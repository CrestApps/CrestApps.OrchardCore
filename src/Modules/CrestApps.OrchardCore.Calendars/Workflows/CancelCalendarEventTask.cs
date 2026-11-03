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
/// Cancels an event, or one occurrence of a repeating event.
/// </summary>
public sealed class CancelCalendarEventTask : CalendarTaskActivity<CancelCalendarEventTask>
{
    private readonly ICalendarEventManager _eventManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelCalendarEventTask"/> class.
    /// </summary>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="workflowService">The calendar workflow service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CancelCalendarEventTask(
        ICalendarEventManager eventManager,
        CalendarWorkflowService workflowService,
        IStringLocalizer<CancelCalendarEventTask> stringLocalizer)
        : base(workflowService, stringLocalizer)
    {
        _eventManager = eventManager;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Cancel Calendar Event"];

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
    /// Gets or sets the reason field, a Liquid expression.
    /// </summary>
    public string Reason
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

        var options = new CalendarChangeOptions
        {
            Actor = CalendarsConstants.WorkflowActor,
            Reason = await Calendars.EvaluateAsync(Reason, workflowContext),
        };

        var occurrence = await Calendars.EvaluateAsync(OccurrenceStart, workflowContext);
        var result = DateTimeOffset.TryParse(occurrence, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var original)
            ? await _eventManager.CancelOccurrenceAsync(calendarEvent, original.UtcDateTime, CalendarEditScope.ThisOccurrence, options)
            : await _eventManager.CancelAsync(calendarEvent, options);

        if (!result.Succeeded)
        {
            return Outcome("Failed");
        }

        workflowContext.Output[GetOutputName("CalendarEvent")] = CalendarWorkflowInput.ToDictionary(result.Value);

        return Outcome("Done");
    }
}
