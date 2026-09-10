using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// A workflow task activity that enqueues a CRM activity into a Contact Center queue so it can be routed
/// to an agent. It lets no-code automations place work items on a queue in response to domain events.
/// </summary>
public sealed class EnqueueActivityTask : TaskActivity<EnqueueActivityTask>
{
    private readonly IActivityQueueService _queueService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnqueueActivityTask"/> class.
    /// </summary>
    /// <param name="queueService">The queue service used to enqueue the activity.</param>
    /// <param name="queueManager">The queue manager used to verify the target queue exists.</param>
    /// <param name="activityManager">The activity manager used to verify the CRM activity exists.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public EnqueueActivityTask(
        IActivityQueueService queueService,
        IActivityQueueManager queueManager,
        IOmnichannelActivityManager activityManager,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<EnqueueActivityTask> logger,
        IStringLocalizer<EnqueueActivityTask> stringLocalizer)
    {
        _queueService = queueService;
        _queueManager = queueManager;
        _activityManager = activityManager;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Enqueue Activity"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the CRM activity identifier to enqueue.
    /// </summary>
    public string ActivityItemId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the target queue identifier.
    /// </summary>
    public string QueueId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional priority override. When null, the queue's default priority is used.
    /// </summary>
    public InteractionPriority? Priority
    {
        get => GetProperty<InteractionPriority?>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return
        [
            new Outcome(S["Done"]),
            new Outcome(S["Failed"]),
        ];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var activityItemId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(ActivityItemId), workflowContext, null))?.Trim();
        var queueId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(QueueId), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(activityItemId) || string.IsNullOrEmpty(queueId))
        {
            _logger.LogWarning("The Enqueue Activity task resolved an empty activity or queue identifier.");

            return WorkflowOutcomeResults.From("Failed");
        }

        try
        {
            var queue = await _queueManager.FindByIdAsync(queueId);

            if (queue is null)
            {
                _logger.LogWarning("The Enqueue Activity task could not find a queue with identifier '{QueueId}'.", queueId);

                return WorkflowOutcomeResults.From("Failed");
            }

            var activity = await _activityManager.FindByIdAsync(activityItemId);

            if (activity is null)
            {
                _logger.LogWarning("The Enqueue Activity task could not find a CRM activity with identifier '{ActivityItemId}'.", activityItemId.SanitizeLogValue());

                return WorkflowOutcomeResults.From("Failed");
            }

            var item = await _queueService.EnqueueAsync(activityItemId, queueId, Priority);

            return item is null
                ? WorkflowOutcomeResults.From("Failed")
                : WorkflowOutcomeResults.From("Done");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while enqueuing activity '{ActivityItemId}' into queue '{QueueId}'.", activityItemId.SanitizeLogValue(), queueId.SanitizeLogValue());

            return WorkflowOutcomeResults.From("Failed");
        }
    }
}
