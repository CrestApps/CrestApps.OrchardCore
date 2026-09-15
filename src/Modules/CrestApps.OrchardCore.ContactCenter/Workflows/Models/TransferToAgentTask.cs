using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// A workflow task activity that moves an automated conversation out of the AI lane and into the human lane: a live
/// call is seated in a queue and offered to an agent, and a text conversation becomes a queue-owned thread in the
/// SMS workspace. It lets a no-code automation escalate on its own conditions rather than waiting for the model to
/// decide it should.
/// </summary>
/// <remarks>
/// The channel is read from the activity and the matching handoff implementation is selected from the registered
/// set, so one activity transfers a call and a text alike, and each channel keeps its own idea of what "hand to an
/// agent" means. The outcome distinguishes a caller who was connected from one who is waiting in the queue, because
/// a workflow usually wants to say something different in each case.
/// </remarks>
public sealed class TransferToAgentTask : TaskActivity<TransferToAgentTask>
{
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IEnumerable<IOmnichannelHandoffService> _handoffServices;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferToAgentTask"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager used to resolve the CRM activity.</param>
    /// <param name="handoffServices">The registered per-channel handoff implementations.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public TransferToAgentTask(
        IOmnichannelActivityManager activityManager,
        IEnumerable<IOmnichannelHandoffService> handoffServices,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<TransferToAgentTask> logger,
        IStringLocalizer<TransferToAgentTask> stringLocalizer)
    {
        _activityManager = activityManager;
        _handoffServices = handoffServices;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Hand Off to Live Agent"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the CRM activity identifier to transfer.
    /// </summary>
    public string ActivityItemId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the target queue identifier. When empty, the subject flow's
    /// configured handoff queue is used.
    /// </summary>
    public string QueueId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression resolving a short reason for the escalation, shown to the agent.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression resolving a summary of the conversation so far, so the receiving agent
    /// inherits the context instead of asking the customer to start over.
    /// </summary>
    public string Summary
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return
        [
            new Outcome(S["Connected"]),
            new Outcome(S["Waiting In Queue"]),
            new Outcome(S["Callback Scheduled"]),
            new Outcome(S["Failed"]),
        ];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var activityItemId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(ActivityItemId), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(activityItemId))
        {
            _logger.LogWarning("The Transfer to Agent task resolved an empty activity identifier.");

            return WorkflowOutcomeResults.From("Failed");
        }

        try
        {
            var activity = await _activityManager.FindByIdAsync(activityItemId);

            if (activity is null)
            {
                _logger.LogWarning("The Transfer to Agent task could not find a CRM activity with identifier '{ActivityItemId}'.", activityItemId.SanitizeLogValue());

                return WorkflowOutcomeResults.From("Failed");
            }

            if (string.IsNullOrEmpty(activity.Channel))
            {
                _logger.LogWarning("The Transfer to Agent task cannot transfer activity '{ActivityItemId}' because it has no channel.", activityItemId.SanitizeLogValue());

                return WorkflowOutcomeResults.From("Failed");
            }

            var handoffService = _handoffServices.FirstOrDefault(x => x.CanHandle(activity.Channel));

            if (handoffService is null)
            {
                // No human destination is configured for this channel (for example the SMS workspace is not
                // enabled). Reporting it rather than throwing lets the workflow choose its own fallback.
                _logger.LogWarning("The Transfer to Agent task found no handoff destination for the '{Channel}' channel.", activity.Channel.SanitizeLogValue());

                return WorkflowOutcomeResults.From("Failed");
            }

            var queueId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(QueueId), workflowContext, null))?.Trim();
            var reason = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Reason), workflowContext, null))?.Trim();
            var summary = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Summary), workflowContext, null))?.Trim();

            var result = await handoffService.RequestHandoffAsync(new OmnichannelHandoffRequest
            {
                Activity = activity,

                // Left empty on purpose when the task does not name a queue: each channel's implementation then
                // falls back to the subject flow's configured handoff queue, which is where an operator already
                // said this work should go.
                TargetQueueId = queueId,
                Reason = string.IsNullOrEmpty(reason)
                    ? "A workflow transferred this conversation to a live agent."
                    : reason,
                Summary = summary,
                ContactAddress = activity.PreferredDestination,

                // Carried across so the answering agent can open the transcript the customer already worked
                // through, rather than making them repeat it.
                AiSessionId = activity.AISessionId,
            });

            if (!result.Succeeded)
            {
                _logger.LogWarning("The Transfer to Agent task did not complete for activity '{ActivityItemId}': {Reason}", activityItemId.SanitizeLogValue(), result.Message);

                return WorkflowOutcomeResults.From("Failed");
            }

            return result.Disposition switch
            {
                HandoffDisposition.WaitingInQueue => WorkflowOutcomeResults.From("Waiting In Queue"),
                HandoffDisposition.CallbackScheduled => WorkflowOutcomeResults.From("Callback Scheduled"),
                _ => WorkflowOutcomeResults.From("Connected"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while transferring the omnichannel activity '{ActivityItemId}' to an agent.", activityItemId.SanitizeLogValue());

            return WorkflowOutcomeResults.From("Failed");
        }
    }
}
