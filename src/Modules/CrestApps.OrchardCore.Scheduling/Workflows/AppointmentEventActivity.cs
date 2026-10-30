using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// A workflow event about an appointment, which can be limited to some appointment types. The workflow input holds
/// <c>Appointment</c> (its fields), <c>AppointmentId</c>, <c>AppointmentTypeId</c>, <c>AppointmentTypeName</c>, <c>StartUtc</c>,
/// <c>EndUtc</c>, <c>CustomerName</c>, <c>CustomerEmail</c>, <c>CustomerPhone</c>, <c>Status</c> and <c>ManageToken</c>.
/// </summary>
public abstract class AppointmentEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected AppointmentEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Scheduling"];

    /// <summary>
    /// Gets or sets the appointment types that start the workflow; empty for every type.
    /// </summary>
    public IList<string> AppointmentTypeIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets how many minutes before the start, for the Starting Soon event.
    /// </summary>
    public int MinutesBefore
    {
        get => GetProperty(() => 60);
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
        if (AppointmentTypeIds.Count == 0)
        {
            return true;
        }

        return workflowContext.Input.TryGetValue("AppointmentTypeId", out var typeId) && AppointmentTypeIds.Contains(typeId?.ToString() ?? string.Empty);
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
}
