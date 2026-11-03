using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// A workflow task that works with calendar events. Its fields are Liquid expressions.
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public abstract class CalendarTaskActivity<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="workflowService">The calendar workflow service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected CalendarTaskActivity(CalendarWorkflowService workflowService, IStringLocalizer stringLocalizer)
    {
        Calendars = workflowService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the calendar workflow service.
    /// </summary>
    protected CalendarWorkflowService Calendars { get; }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Calendar"];

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes, read in Liquid as <c>Workflow.Output.Name</c>.
    /// </summary>
    public string OutputName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets the output name to write, or the task's default.
    /// </summary>
    /// <param name="defaultName">The default.</param>
    /// <returns>The output name.</returns>
    protected string GetOutputName(string defaultName)
    {
        return string.IsNullOrWhiteSpace(OutputName) ? defaultName : OutputName.Trim();
    }
}
