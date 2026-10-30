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
/// Reads an event into the workflow output.
/// </summary>
public sealed class GetCalendarEventTask : CalendarTaskActivity<GetCalendarEventTask>
{
    private readonly ICalendarEventManager _eventManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCalendarEventTask"/> class.
    /// </summary>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="workflowService">The calendar workflow service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GetCalendarEventTask(
        ICalendarEventManager eventManager,
        CalendarWorkflowService workflowService,
        IStringLocalizer<GetCalendarEventTask> stringLocalizer)
        : base(workflowService, stringLocalizer)
    {
        _eventManager = eventManager;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Get Calendar Event"];

    /// <summary>
    /// Gets or sets the event id field, a Liquid expression.
    /// </summary>
    public string EventId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Found"]), new Outcome(S["NotFound"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var calendarEvent = await _eventManager.FindByIdAsync(await Calendars.EvaluateAsync(EventId, workflowContext));

        if (calendarEvent is null)
        {
            return Outcome("NotFound");
        }

        var output = CalendarWorkflowInput.ToDictionary(calendarEvent);

        workflowContext.Output[GetOutputName("CalendarEvent")] = output;
        workflowContext.LastResult = output;

        return Outcome("Found");
    }
}
