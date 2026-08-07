using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// A workflow task activity that sets a Contact Center agent's presence status. It lets no-code
/// automations react to domain events by, for example, placing an agent into wrap-up or on break.
/// </summary>
public sealed class SetAgentPresenceTask : TaskActivity<SetAgentPresenceTask>
{
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetAgentPresenceTask"/> class.
    /// </summary>
    /// <param name="presenceManager">The agent presence manager used to apply the status change.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public SetAgentPresenceTask(
        IAgentPresenceManager presenceManager,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<SetAgentPresenceTask> logger,
        IStringLocalizer<SetAgentPresenceTask> stringLocalizer)
    {
        _presenceManager = presenceManager;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Set Agent Presence"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the Orchard user identifier of the agent.
    /// </summary>
    public string UserId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the presence status to apply to the agent.
    /// </summary>
    public AgentPresenceStatus Status
    {
        get => GetProperty<AgentPresenceStatus>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the reason recorded with the change.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcomes(S["Done"], S["Failed"]);
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (Status is AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy or AgentPresenceStatus.WrapUp)
        {
            _logger.LogWarning("The Set Agent Presence task rejected the reservation- or work-lifecycle-owned status '{Status}'. These states are applied by the contact center runtime and cannot be set by automation.", Status);

            return Outcomes("Failed");
        }

        var userId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(UserId), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("The Set Agent Presence task resolved an empty user identifier.");

            return Outcomes("Failed");
        }

        var reason = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Reason), workflowContext, null))?.Trim();

        try
        {
            var profile = await _presenceManager.SetPresenceAsync(userId, Status, reason);

            return profile is null ? Outcomes("Failed") : Outcomes("Done");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while setting agent presence for user '{UserId}'.", userId.SanitizeLogValue());

            return Outcomes("Failed");
        }
    }
}
