using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Wizard.Workflows.Models;

/// <summary>
/// A workflow event that is raised when a wizard session fails.
/// </summary>
public sealed class WizardFailedEvent : EventActivity
{
    /// <summary>
    /// The technical name of the activity.
    /// </summary>
    public const string EventName = nameof(WizardFailedEvent);

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardFailedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WizardFailedEvent(IStringLocalizer<WizardFailedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => EventName;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Wizard Failed"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Wizard"];

    /// <summary>
    /// Gets or sets an optional wizard type this event is limited to.
    /// </summary>
    public string WizardType
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override ValueTask<IEnumerable<Outcome>> GetPossibleOutcomesAsync(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => ValueTask.FromResult<IEnumerable<Outcome>>(Outcome(S["Done"]));

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
        => Outcome("Done");
}
