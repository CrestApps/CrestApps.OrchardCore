using CrestApps.Core.AI.Guardrails;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Starts a workflow when a guardrail of an AI profile refused what the customer said or the AI's reply.
/// </summary>
public sealed class AIGuardrailTriggeredEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIGuardrailTriggeredEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIGuardrailTriggeredEvent(IStringLocalizer<AIGuardrailTriggeredEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => nameof(AIGuardrailTriggeredEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["AI Guardrail Triggered"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the AI profile this workflow is for, or empty for every profile.
    /// </summary>
    public string ProfileId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => workflowContext.Input.TryGetValue("Guardrail", out var value)
            && value is AIGuardrailVerdict verdict
            && (string.IsNullOrWhiteSpace(ProfileId) || string.Equals(ProfileId, verdict.ProfileId, StringComparison.Ordinal));

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}
