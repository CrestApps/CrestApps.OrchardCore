using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// A workflow event about a calendar event, which can be limited to some calendars, calendar types and event types, and
/// can ignore changes made by workflows so a workflow that changes an event never starts itself again.
/// </summary>
public abstract class CalendarEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected CalendarEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Calendar"];

    /// <summary>
    /// Gets or sets the calendars that start the workflow; empty for every calendar.
    /// </summary>
    public IList<string> CalendarIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the calendar types that start the workflow; empty for every type.
    /// </summary>
    public IList<string> CalendarTypes
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the event types that start the workflow, comma separated; empty for every event type.
    /// </summary>
    public string EventTypes
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whether changes made by workflows are ignored.
    /// </summary>
    public bool IgnoreWorkflowChanges
    {
        get => GetProperty(() => true);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"])];
    }

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var input = workflowContext.Input;

        if (IgnoreWorkflowChanges && string.Equals(Read(input, "Actor"), CalendarsConstants.WorkflowActor, StringComparison.Ordinal))
        {
            return false;
        }

        if (CalendarIds.Count > 0 && !CalendarIds.Contains(Read(input, "CalendarId") ?? string.Empty, StringComparer.Ordinal))
        {
            return false;
        }

        if (CalendarTypes.Count > 0 && !CalendarTypes.Contains(Read(input, "CalendarType") ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var eventTypes = (EventTypes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return eventTypes.Length == 0 || eventTypes.Contains(Read(input, "EventType") ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcome("Done");
    }

    private static string Read(IDictionary<string, object> input, string key)
    {
        return input is not null && input.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
