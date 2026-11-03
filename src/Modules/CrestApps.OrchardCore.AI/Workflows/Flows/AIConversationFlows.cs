using System.Diagnostics;
using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Flows;

/// <summary>
/// Runs conversation flows: a workflow of <c>AI Conversation Stage</c> activities that guides a conversation stage by
/// stage. The flow starts with the conversation's first message, each stage writes itself onto the session and waits,
/// and the AI moves on by calling the <c>moveToStage</c> tool, which resumes the workflow on the chosen outcome.
/// </summary>
public sealed class AIConversationFlows
{
    private readonly IWorkflowTypeStore _workflowTypeStore;
    private readonly IWorkflowStore _workflowStore;
    private readonly IWorkflowManager _workflowManager;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationFlows"/> class.
    /// </summary>
    /// <param name="workflowTypeStore">The workflow type store.</param>
    /// <param name="workflowStore">The workflow instance store.</param>
    /// <param name="workflowManager">The workflow manager.</param>
    /// <param name="sessionManager">The chat session manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AIConversationFlows(
        IWorkflowTypeStore workflowTypeStore,
        IWorkflowStore workflowStore,
        IWorkflowManager workflowManager,
        IAIChatSessionManager sessionManager,
        IClock clock,
        ILogger<AIConversationFlows> logger)
    {
        _workflowTypeStore = workflowTypeStore;
        _workflowStore = workflowStore;
        _workflowManager = workflowManager;
        _sessionManager = sessionManager;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Starts the profile's flow for a conversation that has none yet.
    /// </summary>
    /// <param name="profile">The conversation's profile.</param>
    /// <param name="session">The conversation's session.</param>
    /// <param name="conversation">The conversation, for the flow's input.</param>
    /// <returns>The flow state, or <see langword="null"/> when the profile runs no flow.</returns>
    public async Task<AIConversationFlowState> StartAsync(AIProfile profile, AIChatSession session, AIConversationContext conversation)
    {
        var settings = profile?.GetSettings<AIProfileFlowSettings>();

        if (string.IsNullOrEmpty(settings?.WorkflowTypeId) || session is null || string.IsNullOrEmpty(session.SessionId))
        {
            return null;
        }

        if (session.TryGet<AIConversationFlowState>(out var existing))
        {
            return existing;
        }

        var workflowType = await _workflowTypeStore.GetAsync(settings.WorkflowTypeId);
        var start = workflowType?.Activities.FirstOrDefault(activity => activity.IsStart && activity.Name == nameof(AIConversationFlowStartedEvent));

        if (workflowType is null || !workflowType.IsEnabled || start is null)
        {
            return null;
        }

        var input = AIConversationWorkflowInput.Build(conversation);
        input["SessionId"] = session.SessionId;
        input["ProfileId"] = profile.ItemId;

        // Recorded before the workflow runs, so a stage reached in the same run finds the session and a second request
        // does not start the flow twice.
        session.Put(new AIConversationFlowState { EnteredUtc = _clock.UtcNow });
        await _sessionManager.SaveAsync(session);

        await _workflowManager.StartWorkflowAsync(workflowType, start, input, correlationId: session.SessionId);

        var reloaded = await _sessionManager.FindByIdAsync(session.SessionId);
        var state = reloaded?.GetOrCreate<AIConversationFlowState>();

        if (state is not null)
        {
            session.Put(state);
        }

        return state;
    }

    /// <summary>
    /// Moves a conversation on from its stage by an outcome: resumes the flow's workflow, which runs to the next stage.
    /// </summary>
    /// <param name="sessionId">The conversation's session.</param>
    /// <param name="outcome">The outcome to leave by.</param>
    /// <returns>The flow state after the move, and an error when it could not move.</returns>
    public async Task<(AIConversationFlowState State, string Error)> MoveAsync(string sessionId, string outcome)
    {
        var session = string.IsNullOrEmpty(sessionId) ? null : await _sessionManager.FindByIdAsync(sessionId);

        if (session is null || !session.TryGet<AIConversationFlowState>(out var state) || state.Finished || string.IsNullOrEmpty(state.StageActivityId))
        {
            return (null, "This conversation is not in a stage of a flow.");
        }

        var chosen = state.Outcomes.FirstOrDefault(candidate => string.Equals(candidate.Name, outcome?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (chosen is null)
        {
            return (state, $"'{outcome}' is not a way out of the stage. Use one of: {string.Join(", ", state.Outcomes.Select(candidate => candidate.Name))}.");
        }

        var workflow = await _workflowStore.GetAsync(state.WorkflowId);
        var blocking = workflow?.BlockingActivities.FirstOrDefault(activity => activity.ActivityId == state.StageActivityId);

        if (blocking is null)
        {
            return (state, "The flow is no longer waiting at this stage.");
        }

        // The stage is left by this outcome before the workflow moves on, so the next stage appends after it.
        var stepIndex = state.History.Count - 1;

        if (stepIndex >= 0)
        {
            state.History[stepIndex].Outcome = chosen.Name;
        }

        var enteredBefore = state.EnteredUtc;
        session.Put(state);
        await _sessionManager.SaveAsync(session);

        var elapsed = Stopwatch.StartNew();

        await _workflowManager.ResumeWorkflowAsync(workflow, blocking, new Dictionary<string, object>
        {
            [AIConversationStageTask.OutcomeInput] = chosen.Name,
            ["SessionId"] = sessionId,
        });

        elapsed.Stop();

        var reloaded = await _sessionManager.FindByIdAsync(sessionId);
        var after = reloaded?.GetOrCreate<AIConversationFlowState>() ?? state;

        // How long the move took the workflow: the delay a caller hears between stages.
        if (stepIndex >= 0 && stepIndex < after.History.Count)
        {
            after.History[stepIndex].TransitionMs = elapsed.ElapsedMilliseconds;
        }

        // A workflow that did not reach another stage has finished the flow.
        if (after.EnteredUtc == enteredBefore && after.StageActivityId == state.StageActivityId)
        {
            after.Finished = true;
            after.StageActivityId = null;
            after.Outcomes = [];
        }

        if (reloaded is not null)
        {
            reloaded.Put(after);
            await _sessionManager.SaveAsync(reloaded);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Conversation '{SessionId}' left stage '{Stage}' by '{Outcome}' in {Milliseconds} ms.",
                sessionId.SanitizeLogValue(),
                state.StageName.SanitizeLogValue(),
                chosen.Name.SanitizeLogValue(),
                elapsed.ElapsedMilliseconds);
        }

        return (after, null);
    }

    /// <summary>
    /// Every stage of a flow, for flexible mode.
    /// </summary>
    /// <param name="profile">The profile running the flow.</param>
    /// <returns>Each stage's name and instructions.</returns>
    public async Task<IReadOnlyList<(string Name, string Instructions)>> GetStagesAsync(AIProfile profile)
    {
        var settings = profile?.GetSettings<AIProfileFlowSettings>();

        if (string.IsNullOrEmpty(settings?.WorkflowTypeId))
        {
            return [];
        }

        var workflowType = await _workflowTypeStore.GetAsync(settings.WorkflowTypeId);

        return workflowType?.Activities
            .Where(activity => activity.Name == nameof(AIConversationStageTask))
            .Select(activity => (
                Name: activity.Properties?[nameof(AIConversationStageTask.StageName)]?.ToString() ?? activity.ActivityId,
                Instructions: activity.Properties?[nameof(AIConversationStageTask.Instructions)]?.ToString()))
            .ToList() ?? [];
    }
}
