using System.Globalization;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts a workflow a set number of minutes before each occurrence of a calendar event starts. Needs the Calendar Reminders
/// feature.
/// </summary>
public sealed class CalendarEventStartingSoonEvent : CalendarEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventStartingSoonEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarEventStartingSoonEvent(IStringLocalizer<CalendarEventStartingSoonEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CalendarEventStartingSoonEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calendar Event Starting Soon"];

    /// <summary>
    /// Gets or sets how many minutes before the start the workflow starts.
    /// </summary>
    public int MinutesBefore
    {
        get => GetProperty(() => 15);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return base.CanExecute(workflowContext, activityContext) &&
            workflowContext.Input.TryGetValue("MinutesBefore", out var minutes) &&
            Convert.ToInt32(minutes, CultureInfo.InvariantCulture) == MinutesBefore;
    }
}
