using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// A workflow event that starts or resumes a workflow when a Contact Center domain event is published.
/// The optional <see cref="EventType"/> filters the activity to a single event type.
/// </summary>
public sealed class ContactCenterEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterEvent(IStringLocalizer<ContactCenterEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => nameof(ContactCenterEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Contact Center Event"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the domain event type this activity reacts to. When empty, it reacts to every event.
    /// </summary>
    public string EventType
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override ValueTask<IEnumerable<Outcome>> GetPossibleOutcomesAsync(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        return new ValueTask<IEnumerable<Outcome>>(
        [
            Outcome(S["Matched"]),
            Outcome(S["Ignored"]),
        ]);
    }

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        return Evaluate(workflowContext);
    }

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(
        WorkflowExecutionContext workflowContext,
        ActivityContext activityContext)
    {
        return Evaluate(workflowContext);
    }

    private ActivityExecutionResult Evaluate(WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrEmpty(EventType))
        {
            return Outcomes("Matched");
        }

        if (workflowContext.Input.TryGetValue("EventType", out var value) &&
            string.Equals(value?.ToString(), EventType, StringComparison.OrdinalIgnoreCase))
        {
            return Outcomes("Matched");
        }

        return Outcomes("Ignored");
    }
}
