using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Evaluates a stored conversation, or a set of turns such as a simulated one, and keeps the result.
/// </summary>
public sealed class AIConversationEvaluationService
{
    private readonly IAIConversationEvaluator _evaluator;
    private readonly IAIConversationEvaluationStore _store;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIProfileManager _profileManager;
    private readonly IAICompletionContextBuilder _contextBuilder;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationEvaluationService"/> class.
    /// </summary>
    /// <param name="evaluator">The evaluator that judges a conversation.</param>
    /// <param name="store">The store evaluations are kept in.</param>
    /// <param name="sessionManager">The AI session manager.</param>
    /// <param name="promptStore">The store of a session's turns.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="contextBuilder">The completion context builder, which gives the profile's instructions.</param>
    /// <param name="logger">The logger.</param>
    public AIConversationEvaluationService(
        IAIConversationEvaluator evaluator,
        IAIConversationEvaluationStore store,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        IAICompletionContextBuilder contextBuilder,
        ILogger<AIConversationEvaluationService> logger)
    {
        _evaluator = evaluator;
        _store = store;
        _sessionManager = sessionManager;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates a stored conversation and saves the evaluation.
    /// </summary>
    /// <param name="sessionId">The AI session.</param>
    /// <param name="criteria">The criteria, one per entry.</param>
    /// <param name="passingScore">The lowest score that passes.</param>
    /// <param name="label">A label that groups the evaluation.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The evaluation, or <see langword="null"/> when the session is missing, has no customer turn, or no
    /// deployment can judge.</returns>
    public async Task<AIConversationEvaluation> EvaluateSessionAsync(
        string sessionId,
        IReadOnlyList<string> criteria,
        int passingScore,
        string label,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var session = await _sessionManager.FindByIdAsync(sessionId.Trim(), cancellationToken);

        if (session is null)
        {
            return null;
        }

        var profile = string.IsNullOrEmpty(session.ProfileId)
            ? null
            : await _profileManager.FindByIdAsync(session.ProfileId, cancellationToken);

        var turns = AIConversationTranscript.GetTurns(await _promptStore.GetPromptsAsync(session.SessionId));

        return await EvaluateTurnsAsync(profile, session, turns, criteria, passingScore, label, null, cancellationToken);
    }

    /// <summary>
    /// Evaluates a conversation that is not stored, such as a simulated one, and saves the evaluation.
    /// </summary>
    /// <param name="profile">The AI profile that answered, or <see langword="null"/>.</param>
    /// <param name="session">The AI session, or <see langword="null"/>.</param>
    /// <param name="turns">The turns.</param>
    /// <param name="criteria">The criteria, one per entry.</param>
    /// <param name="passingScore">The lowest score that passes.</param>
    /// <param name="label">A label that groups the evaluation.</param>
    /// <param name="instructions">The instructions the AI had, or <see langword="null"/> to read the profile's.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<AIConversationEvaluation> EvaluateTurnsAsync(
        AIProfile profile,
        AIChatSession session,
        IReadOnlyList<AIConversationTurn> turns,
        IReadOnlyList<string> criteria,
        int passingScore,
        string label,
        string instructions,
        CancellationToken cancellationToken = default)
    {
        instructions ??= await GetInstructionsAsync(profile, cancellationToken);

        var evaluation = await _evaluator.EvaluateAsync(new AIConversationEvaluationRequest
        {
            Turns = turns,
            Criteria = criteria ?? [],
            PassingScore = passingScore,
            Instructions = instructions,
        }, cancellationToken);

        if (evaluation is null)
        {
            return null;
        }

        evaluation.ItemId = UniqueId.GenerateId();
        evaluation.SessionId = session?.SessionId;
        evaluation.ProfileId = profile?.ItemId ?? session?.ProfileId;
        evaluation.ProfileVersion = session is not null
            ? (session.TryGet<AIProfileVersionMetadata>(out var sessionVersion) ? sessionVersion.Version : null)
            : (profile is not null && profile.TryGet<AIProfileVersionMetadata>(out var profileVersion) ? profileVersion.Version : null);
        evaluation.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        await _store.SaveAsync(evaluation, cancellationToken);

        return evaluation;
    }

    private async Task<string> GetInstructionsAsync(AIProfile profile, CancellationToken cancellationToken)
    {
        if (profile is null)
        {
            return null;
        }

        try
        {
            var context = await _contextBuilder.BuildAsync(profile, cancellationToken: cancellationToken);

            return context?.SystemMessage;
        }
        catch (Exception ex)
        {
            // The judge still scores the conversation without them; it just cannot spot broken instructions.
            _logger.LogWarning(ex, "Could not read the instructions of AI profile '{ProfileId}' for an evaluation.", profile.ItemId);

            return null;
        }
    }
}
