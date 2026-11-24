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
        return ValueTask.FromResult<IEnumerable<Outcome>>(
        [
            new Outcome(S["Matched"]),
            new Outcome(S["Ignored"]),
        ]);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every Contact Center event, agent heartbeats included, is offered to every workflow that starts on this activity.
    /// A workflow started for an event of another type only took the Ignored outcome, yet it was saved as a finished
    /// instance: one production workflow kept several hundred thousand of them, several written every few seconds. An
    /// event of another type now starts no instance at all, so nothing is written. A workflow already waiting on this
    /// activity still resumes and takes Ignored, as before.
    /// </remarks>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (workflowContext.Status != WorkflowStatus.Starting || activityContext?.ActivityRecord?.IsStart != true)
        {
            return true;
        }

        return Matches(workflowContext);
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
        => WorkflowOutcomeResults.From(Matches(workflowContext) ? "Matched" : "Ignored");

    private bool Matches(WorkflowExecutionContext workflowContext)
        => string.IsNullOrEmpty(EventType) ||
            (workflowContext.Input.TryGetValue("EventType", out var value) &&
            string.Equals(value?.ToString(), EventType, StringComparison.OrdinalIgnoreCase));
}
