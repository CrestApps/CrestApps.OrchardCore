using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Scores a QA cohort's sample of closed conversations: each conversation of the cohort's profiles is considered once,
/// sampled by a stable hash of its identifier, and the sampled ones are evaluated against the cohort's criteria, with
/// the knowledge-base check when the cohort asks for it.
/// </summary>
public sealed class AIQACohortEvaluator
{
    /// <summary>
    /// The most conversations one pass considers per cohort.
    /// </summary>
    public const int BatchSize = 50;

    private readonly ISession _session;
    private readonly YesSqlStoreOptions _storeOptions;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIProfileManager _profileManager;
    private readonly AIConversationEvaluationService _evaluationService;
    private readonly AIConversationKnowledgeCollector _knowledgeCollector;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQACohortEvaluator"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="storeOptions">The YesSql store options, which name the AI collection.</param>
    /// <param name="promptStore">The chat session prompt store.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="evaluationService">Scores the conversations.</param>
    /// <param name="knowledgeCollector">Finds what the knowledge base holds on what was asked.</param>
    /// <param name="logger">The logger.</param>
    public AIQACohortEvaluator(
        ISession session,
        IOptions<YesSqlStoreOptions> storeOptions,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        AIConversationEvaluationService evaluationService,
        AIConversationKnowledgeCollector knowledgeCollector,
        ILogger<AIQACohortEvaluator> logger)
    {
        _session = session;
        _storeOptions = storeOptions.Value;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _evaluationService = evaluationService;
        _knowledgeCollector = knowledgeCollector;
        _logger = logger;
    }

    /// <summary>
    /// Whether a conversation falls in a cohort's sample. Stable, so a conversation is in or out for good.
    /// </summary>
    /// <param name="cohortId">The cohort.</param>
    /// <param name="sessionId">The conversation.</param>
    /// <param name="samplePercent">The share sampled, from 1 to 100.</param>
    /// <returns><see langword="true"/> when the conversation is scored.</returns>
    public static bool IsSampled(string cohortId, string sessionId, int samplePercent)
    {
        if (samplePercent >= 100)
        {
            return true;
        }

        if (samplePercent <= 0 || string.IsNullOrEmpty(sessionId))
        {
            return false;
        }

        // FNV-1a over the characters: stable across processes and versions, unlike string.GetHashCode.
        var hash = 2166136261u;

        foreach (var character in $"{cohortId}:{sessionId}")
        {
            hash = (hash ^ character) * 16777619u;
        }

        return hash % 100 < samplePercent;
    }

    /// <summary>
    /// Scores the cohort's next conversations and moves its cursor past them.
    /// </summary>
    /// <param name="cohort">The cohort.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The evaluations made.</returns>
    public async Task<IReadOnlyList<AIConversationEvaluation>> EvaluateNextAsync(AIQACohort cohort, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cohort);

        var evaluations = new List<AIConversationEvaluation>();

        if (!cohort.Enabled || cohort.ProfileIds is not { Count: > 0 })
        {
            return evaluations;
        }

        var profileIds = cohort.ProfileIds.ToArray();
        var cursor = cohort.CursorUtc ?? DateTime.MinValue;

        var sessions = await _session.Query<AIChatSession, AIChatSessionIndex>(
                index => index.ProfileId.IsIn(profileIds) && index.Status == ChatSessionStatus.Closed && index.LastActivityUtc > cursor,
                _storeOptions.AICollectionName)
            .OrderBy(index => index.LastActivityUtc)
            .Take(BatchSize)
            .ListAsync(cancellationToken);

        var criteria = AIConversationEvaluator.ParseCriteria(cohort.Criteria);

        foreach (var chatSession in sessions)
        {
            cohort.CursorUtc = chatSession.LastActivityUtc;

            if (!IsSampled(cohort.ItemId, chatSession.SessionId, cohort.SamplePercent))
            {
                continue;
            }

            try
            {
                var profile = await _profileManager.FindByIdAsync(chatSession.ProfileId, cancellationToken);
                var turns = AIConversationTranscript.GetTurns(await _promptStore.GetPromptsAsync(chatSession.SessionId));
                var knowledge = cohort.CheckKnowledge
                    ? await _knowledgeCollector.CollectAsync(profile, turns, cancellationToken)
                    : null;

                var evaluation = await _evaluationService.EvaluateTurnsAsync(
                    profile,
                    chatSession,
                    turns,
                    criteria,
                    cohort.PassingScore,
                    cohort.GetLabel(),
                    instructions: null,
                    cohort.JudgeDeploymentName,
                    knowledge,
                    cancellationToken);

                if (evaluation is not null)
                {
                    evaluations.Add(evaluation);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Conversation '{SessionId}' could not be scored for QA cohort '{CohortId}'.", chatSession.SessionId.SanitizeLogValue(), cohort.ItemId.SanitizeLogValue());
            }
        }

        return evaluations;
    }
}
