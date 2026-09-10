using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
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
/// A workflow task activity that starts an automated omnichannel activity now: it places the outbound call for a
/// Phone activity, or sends the opening message for an SMS activity. It lets a no-code automation reach a customer
/// in response to a domain event, instead of waiting for the periodic automated-activities pass to pick the work up.
/// </summary>
/// <remarks>
/// The channel is read from the activity rather than chosen on the task, so one activity covers both placing a call
/// and opening a text conversation, and a workflow keeps working if the activity's channel changes.
/// </remarks>
public sealed class StartOmnichannelActivityTask : TaskActivity<StartOmnichannelActivityTask>
{
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IEnumerable<IOmnichannelProcessor> _processors;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartOmnichannelActivityTask"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager used to resolve the CRM activity.</param>
    /// <param name="processors">The registered channel processors, one of which starts the activity.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public StartOmnichannelActivityTask(
        IOmnichannelActivityManager activityManager,
        IEnumerable<IOmnichannelProcessor> processors,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<StartOmnichannelActivityTask> logger,
        IStringLocalizer<StartOmnichannelActivityTask> stringLocalizer)
    {
        _activityManager = activityManager;
        _processors = processors;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Place Call or Send Message"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the CRM activity identifier to start.
    /// </summary>
    public string ActivityItemId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return
        [
            new Outcome(S["Done"]),
            new Outcome(S["Already Started"]),
            new Outcome(S["Failed"]),
        ];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var activityItemId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(ActivityItemId), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(activityItemId))
        {
            _logger.LogWarning("The Place Call or Send Message task resolved an empty activity identifier.");

            return WorkflowOutcomeResults.From("Failed");
        }

        try
        {
            var activity = await _activityManager.FindByIdAsync(activityItemId);

            if (activity is null)
            {
                _logger.LogWarning("The Place Call or Send Message task could not find a CRM activity with identifier '{ActivityItemId}'.", activityItemId.SanitizeLogValue());

                return WorkflowOutcomeResults.From("Failed");
            }

            // Only work that has not been reached yet may be started. Starting an activity that is already dialing,
            // connected, or awaiting an answer would place a second call to a customer who is on the line with us —
            // so a workflow that fires twice, or races the periodic automated-activities pass, takes the
            // "Already Started" branch instead of dialing again. This mirrors the due-set filter that pass uses.
            if (activity.Status is not (ActivityStatus.NotStated or ActivityStatus.Scheduled))
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("The Place Call or Send Message task skipped activity '{ActivityItemId}' because it is already '{Status}'.", activityItemId.SanitizeLogValue(), activity.Status);
                }

                return WorkflowOutcomeResults.From("Already Started");
            }

            if (string.IsNullOrEmpty(activity.Channel))
            {
                _logger.LogWarning("The Place Call or Send Message task cannot start activity '{ActivityItemId}' because it has no channel.", activityItemId.SanitizeLogValue());

                return WorkflowOutcomeResults.From("Failed");
            }

            var processor = _processors.FirstOrDefault(x => string.Equals(x.Channel, activity.Channel, StringComparison.OrdinalIgnoreCase));

            if (processor is null)
            {
                // The channel's module is not enabled (for example a Phone activity on a tenant without voice).
                _logger.LogWarning("The Place Call or Send Message task found no processor for the '{Channel}' channel.", activity.Channel.SanitizeLogValue());

                return WorkflowOutcomeResults.From("Failed");
            }

            await processor.StartAsync(activity, CancellationToken.None);

            return WorkflowOutcomeResults.From("Done");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while starting the omnichannel activity '{ActivityItemId}'.", activityItemId.SanitizeLogValue());

            return WorkflowOutcomeResults.From("Failed");
        }
    }
}
