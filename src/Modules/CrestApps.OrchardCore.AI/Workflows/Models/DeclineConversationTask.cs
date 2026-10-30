using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// In an <c>AI Conversation Starting</c> workflow, stops the conversation from taking place: an outbound call or
/// text is not sent, and an inbound call goes to the entry point's fallback.
/// </summary>
public sealed class DeclineConversationTask : TaskActivity<DeclineConversationTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclineConversationTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator, which renders the reason.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DeclineConversationTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<DeclineConversationTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Decline Conversation"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets why the conversation is declined, as a Liquid template, kept on the record.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Not starting"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (!workflowContext.Input.TryGetValue(AIConversationWorkflowInput.StartingResult, out var value) ||
            value is not AIConversationStartingResult starting)
        {
            return Outcome("Not starting");
        }

        starting.Declined = true;
        starting.DeclineReason = string.IsNullOrWhiteSpace(Reason)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Reason), workflowContext, null))?.Trim();

        return Outcome("Done");
    }
}
