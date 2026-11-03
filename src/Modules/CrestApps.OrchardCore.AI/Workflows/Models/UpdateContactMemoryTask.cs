using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Workflows.Models;

/// <summary>
/// Rewrites what the AI remembers about a contact from a finished conversation, whatever the profile's own contact
/// memory setting is.
/// </summary>
public sealed class UpdateContactMemoryTask : TaskActivity<UpdateContactMemoryTask>
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IAIChatSessionManager _sessionManager;
    private readonly IAIChatSessionPromptStore _promptStore;
    private readonly AIContactMemoryService _memoryService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateContactMemoryTask"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="sessionManager">The AI session manager.</param>
    /// <param name="promptStore">The store of a session's turns.</param>
    /// <param name="memoryService">The contact memory service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UpdateContactMemoryTask(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore,
        AIContactMemoryService memoryService,
        ILogger<UpdateContactMemoryTask> logger,
        IStringLocalizer<UpdateContactMemoryTask> stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        _sessionManager = sessionManager;
        _promptStore = promptStore;
        _memoryService = memoryService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Update Contact Memory"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets a Liquid expression for the AI session to remember.
    /// </summary>
    public string SessionId
    {
        get => GetProperty(() => "{{ Workflow.Input.SessionId }}");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a Liquid expression for the contact, or empty for the contact the conversation was with.
    /// </summary>
    public string ContactId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Updated"]), new Outcome(S["Nothing to remember"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var sessionId = (await RenderAsync(SessionId, workflowContext))?.Trim();
        var session = string.IsNullOrEmpty(sessionId) ? null : await _sessionManager.FindByIdAsync(sessionId);

        if (session is null)
        {
            return Outcome("Nothing to remember");
        }

        var contactId = (await RenderAsync(ContactId, workflowContext))?.Trim();

        if (string.IsNullOrEmpty(contactId) && session.TryGet<AIConversationContactMetadata>(out var contact))
        {
            contactId = contact.ContactId;
        }

        if (string.IsNullOrEmpty(contactId))
        {
            return Outcome("Nothing to remember");
        }

        try
        {
            var turns = AIConversationTranscript.GetTurns(await _promptStore.GetPromptsAsync(session.SessionId));
            var memory = await _memoryService.RememberAsync(contactId, turns, session.SessionId);

            if (memory is null)
            {
                return Outcome("Nothing to remember");
            }

            workflowContext.Output["ContactMemory"] = memory;
            workflowContext.LastResult = memory;

            return Outcome("Updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Updating the memory of contact '{ContactId}' from AI session '{SessionId}' failed.", contactId, session.SessionId);

            return Outcome("Nothing to remember");
        }
    }

    private async Task<string> RenderAsync(string template, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(template)
            ? null
            : await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(template), workflowContext, null);
}
