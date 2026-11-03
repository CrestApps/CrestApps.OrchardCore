using CrestApps.Core.AI.Chat.Privacy;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Redacts or clears a conversation's evaluations with the conversation, and deletes them with it.
/// </summary>
public sealed class AIConversationEvaluationPrivacyHandler : AIConversationPrivacyHandlerBase
{
    private const string EvaluationsKey = "AIConversationEvaluations";

    private readonly IAIConversationEvaluationStore _evaluationStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationEvaluationPrivacyHandler"/> class.
    /// </summary>
    /// <param name="evaluationStore">The evaluation store.</param>
    public AIConversationEvaluationPrivacyHandler(IAIConversationEvaluationStore evaluationStore)
    {
        _evaluationStore = evaluationStore;
    }

    /// <inheritdoc/>
    public override async Task CollectingAsync(AIConversationPrivacyContext context, CancellationToken cancellationToken)
    {
        var evaluations = await _evaluationStore.ListBySessionAsync(context.Session.SessionId, cancellationToken);

        context.Items[EvaluationsKey] = evaluations;

        foreach (var evaluation in evaluations)
        {
            context.Texts.Add(evaluation.Summary);

            foreach (var issue in evaluation.Issues)
            {
                context.Texts.Add(issue.Quote);
            }
        }
    }

    /// <inheritdoc/>
    public override async Task ApplyingAsync(AIConversationPrivacyContext context, CancellationToken cancellationToken)
    {
        if (!context.Items.TryGetValue(EvaluationsKey, out var value) || value is not IReadOnlyList<AIConversationEvaluation> evaluations)
        {
            return;
        }

        foreach (var evaluation in evaluations)
        {
            if (context.Mode == AIDataStorageMode.MetadataOnly)
            {
                Strip(evaluation);
            }
            else
            {
                Redact(evaluation, context);
            }

            await _evaluationStore.SaveAsync(evaluation, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override Task DeletingAsync(AIChatSession session, CancellationToken cancellationToken)
        => _evaluationStore.DeleteBySessionAsync(session.SessionId, cancellationToken);

    internal static void Redact(AIConversationEvaluation evaluation, AIConversationPrivacyContext context)
    {
        evaluation.Summary = context.Redact(evaluation.Summary);

        foreach (var criterion in evaluation.Criteria)
        {
            criterion.Reason = context.Redact(criterion.Reason);
        }

        foreach (var issue in evaluation.Issues)
        {
            issue.Detail = context.Redact(issue.Detail);
            issue.Quote = context.Redact(issue.Quote);
        }
    }

    internal static void Strip(AIConversationEvaluation evaluation)
    {
        evaluation.Summary = null;

        foreach (var criterion in evaluation.Criteria)
        {
            criterion.Reason = null;
        }

        foreach (var issue in evaluation.Issues)
        {
            issue.Detail = null;
            issue.Quote = null;
        }
    }
}
