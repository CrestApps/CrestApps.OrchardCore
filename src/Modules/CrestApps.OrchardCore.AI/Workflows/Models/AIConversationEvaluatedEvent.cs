using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Starts a workflow when an AI conversation, real or simulated, has been evaluated.
/// </summary>
public sealed class AIConversationEvaluatedEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationEvaluatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIConversationEvaluatedEvent(IStringLocalizer<AIConversationEvaluatedEvent> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => nameof(AIConversationEvaluatedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["AI Conversation Evaluated"];

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

    /// <summary>
    /// Gets or sets whether only evaluations that failed start the workflow.
    /// </summary>
    public bool OnlyFailures
    {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (!workflowContext.Input.TryGetValue("Evaluation", out var value) || value is not AIConversationEvaluation evaluation)
        {
            return false;
        }

        return (string.IsNullOrWhiteSpace(ProfileId) || string.Equals(ProfileId, evaluation.ProfileId, StringComparison.Ordinal))
            && (!OnlyFailures || !evaluation.Passed);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <summary>
    /// Starts the workflows waiting for an evaluation. The input carries the <c>Evaluation</c>, its
    /// <c>SessionId</c>, <c>ProfileId</c>, <c>Score</c>, <c>Passed</c> and <c>Label</c>.
    /// </summary>
    /// <param name="serviceProvider">The scope's service provider.</param>
    /// <param name="evaluation">The evaluation.</param>
    /// <param name="logger">The logger a failure is written to.</param>
    internal static async Task TriggerAsync(IServiceProvider serviceProvider, AIConversationEvaluation evaluation, ILogger logger)
    {
        await AIWebhookPublisher.TryPublishAsync(serviceProvider, AIWebhookEvents.ConversationEvaluated, new
        {
            evaluationId = evaluation.ItemId,
            sessionId = evaluation.SessionId,
            profileId = evaluation.ProfileId,
            profileVersion = evaluation.ProfileVersion,
            label = evaluation.Label,
            score = evaluation.Score,
            passed = evaluation.Passed,
            resolved = evaluation.Resolved,
            sentiment = evaluation.Sentiment,
            summary = evaluation.Summary,
            issues = evaluation.Issues,
        });

        var workflowManager = serviceProvider.GetService<IWorkflowManager>();

        if (workflowManager is null)
        {
            return;
        }

        try
        {
            await workflowManager.TriggerEventAsync(
                nameof(AIConversationEvaluatedEvent),
                new Dictionary<string, object>
                {
                    ["Evaluation"] = evaluation,
                    ["SessionId"] = evaluation.SessionId,
                    ["ProfileId"] = evaluation.ProfileId,
                    ["Score"] = evaluation.Score,
                    ["Passed"] = evaluation.Passed,
                    ["Label"] = evaluation.Label,
                },
                correlationId: evaluation.SessionId ?? evaluation.ItemId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Starting the AI Conversation Evaluated workflows for evaluation '{EvaluationId}' failed.", evaluation.ItemId);
        }
    }
}
