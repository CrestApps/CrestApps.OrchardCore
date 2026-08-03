using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// A workflow task activity that schedules a Contact Center callback. It lets no-code automations create a
/// customer callback in response to domain events, such as scheduling a callback after an abandoned call.
/// </summary>
public sealed class ScheduleCallbackTask : TaskActivity<ScheduleCallbackTask>
{
    private readonly ICallbackService _callbackService;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleCallbackTask"/> class.
    /// </summary>
    /// <param name="callbackService">The callback service used to schedule the callback.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="clock">The clock used to compute the scheduled time.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public ScheduleCallbackTask(
        ICallbackService callbackService,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IClock clock,
        ILogger<ScheduleCallbackTask> logger,
        IStringLocalizer<ScheduleCallbackTask> stringLocalizer)
    {
        _callbackService = callbackService;
        _expressionEvaluator = expressionEvaluator;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Schedule Callback"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the destination number or address to call back.
    /// </summary>
    public string Destination
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the delay, in minutes from now, before the callback becomes due. Zero schedules it immediately.
    /// </summary>
    public int DelayMinutes
    {
        get => GetProperty<int>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the campaign the callback belongs to.
    /// </summary>
    public string CampaignId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the queue the promoted activity is enqueued into.
    /// </summary>
    public string QueueId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the optional Liquid expression that resolves the content item identifier of the contact.
    /// </summary>
    public string ContactContentItemId
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
        var destination = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Destination), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(destination))
        {
            _logger.LogWarning("The Schedule Callback task resolved an empty destination.");

            return Outcomes("Failed");
        }

        var callback = new CallbackRequest
        {
            Destination = destination,
            CampaignId = await ResolveOptionalAsync(CampaignId, workflowContext),
            QueueId = await ResolveOptionalAsync(QueueId, workflowContext),
            ContactContentItemId = await ResolveOptionalAsync(ContactContentItemId, workflowContext),
        };

        if (DelayMinutes > 0)
        {
            callback.ScheduledUtc = _clock.UtcNow.AddMinutes(DelayMinutes);
        }

        try
        {
            await _callbackService.ScheduleAsync(callback);

            return Outcomes("Done");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while scheduling a callback to '{Destination}'.", destination);

            return Outcomes("Failed");
        }
    }

    private async Task<string> ResolveOptionalAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
