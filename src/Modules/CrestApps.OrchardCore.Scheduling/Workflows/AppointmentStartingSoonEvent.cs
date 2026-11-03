using System.Globalization;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts a set number of minutes before an appointment. Needs the Calendar Reminders feature.
/// </summary>
public sealed class AppointmentStartingSoonEvent : AppointmentEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentStartingSoonEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentStartingSoonEvent(IStringLocalizer<AppointmentStartingSoonEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(AppointmentStartingSoonEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Appointment Starting Soon"];

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return base.CanExecute(workflowContext, activityContext) &&
            workflowContext.Input.TryGetValue("MinutesBefore", out var minutes) &&
            Convert.ToInt32(minutes, CultureInfo.InvariantCulture) == MinutesBefore;
    }
}
