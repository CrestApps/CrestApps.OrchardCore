using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Runs a finished conversation's post-session analysis again, with the profile's current tasks and conversion
/// goals, for example after a task was added or its instructions changed.
/// </summary>
public sealed class RerunConversationAnalysisTask : TaskActivity<RerunConversationAnalysisTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly IAIProfileManager _profileManager;
    private readonly AIChatSessionPostCloseProcessor _postCloseProcessor;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RerunConversationAnalysisTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="sessionManager">The AI session manager.</param>
    /// <param name="promptStore">The store of a session's turns.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="postCloseProcessor">The processor that runs the analysis.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RerunConversationAnalysisTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        IAIProfileManager profileManager,
        AIChatSessionPostCloseProcessor postCloseProcessor,
        ILogger<RerunConversationAnalysisTask> logger,
        IStringLocalizer<RerunConversationAnalysisTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _sessionManager = sessionManager;
        _promptStore = promptStore;
        _profileManager = profileManager;
        _postCloseProcessor = postCloseProcessor;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Rerun AI Conversation Analysis"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets a Liquid expression for the AI session to analyze again.
    /// </summary>
    public string SessionId
    {
        get => GetProperty(() => "{{ Workflow.Input.SessionId }}");
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Nothing to analyze"]), new Outcome(S["Not found"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var sessionId = string.IsNullOrWhiteSpace(SessionId)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(SessionId), workflowContext, null))?.Trim();

        var session = string.IsNullOrEmpty(sessionId) ? null : await _sessionManager.FindByIdAsync(sessionId);
        var profile = string.IsNullOrEmpty(session?.ProfileId) ? null : await _profileManager.FindByIdAsync(session.ProfileId);

        if (session is null || profile is null)
        {
            return Outcome("Not found");
        }

        Reset(session);

        if (!_postCloseProcessor.QueueIfNeeded(profile, session))
        {
            return Outcome("Nothing to analyze");
        }

        try
        {
            var prompts = await _promptStore.GetPromptsAsync(session.SessionId);
            await _postCloseProcessor.ProcessAsync(profile, session, prompts);
        }
        finally
        {
            // Whatever was redone is kept; what failed is retried by the session close task like any other.
            await _sessionManager.SaveAsync(session);
        }

        workflowContext.Output["Results"] = session.PostSessionResults;
        workflowContext.Output["Variables"] = new Dictionary<string, string>(session.GetConversationVariables(), StringComparer.OrdinalIgnoreCase);
        workflowContext.LastResult = session.PostSessionResults;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Reran the analysis of AI session '{SessionId}'.", session.SessionId);
        }

        return Outcome("Done");
    }

    /// <summary>
    /// Forgets a session's analysis so it is all done again: the post-session task results, the conversion goals
    /// and the attempts. The session metrics are kept, so a rerun does not count the session twice.
    /// </summary>
    /// <param name="session">The session.</param>
    internal static void Reset(CrestApps.Core.AI.Models.AIChatSession session)
    {
        session.PostSessionResults.Clear();
        session.IsPostSessionTasksProcessed = false;
        session.IsConversionGoalsEvaluated = false;
        session.PostSessionProcessingAttempts = 0;
        session.PostSessionProcessingLastAttemptUtc = null;
    }
}
