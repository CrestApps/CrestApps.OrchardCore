using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// A workflow task that tries a completed dialer campaign record again: it creates the next attempt and queues it in
/// the campaign it was dialed from, so a workflow reacting to <c>ActivityDispositionApplied</c> decides when the dialer
/// calls next.
/// </summary>
public sealed class ScheduleDialerRetryTask : TaskActivity<ScheduleDialerRetryTask>
{
    private readonly IDialerRetryScheduler _retryScheduler;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleDialerRetryTask"/> class.
    /// </summary>
    /// <param name="retryScheduler">The scheduler that creates and queues the next attempt.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public ScheduleDialerRetryTask(
        IDialerRetryScheduler retryScheduler,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<ScheduleDialerRetryTask> logger,
        IStringLocalizer<ScheduleDialerRetryTask> stringLocalizer)
    {
        _retryScheduler = retryScheduler;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Schedule Dialer Retry"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the completed dialer activity to try again.
    /// </summary>
    public string ActivityItemId
    {
        get => GetProperty(() => "{{ Workflow.Input.Data.ActivityItemId }}");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves how many minutes to wait before the next attempt. Empty uses
    /// the dialer profile's retry delay; a shorter delay than the profile's is raised to it.
    /// </summary>
    public string DelayMinutes
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return
        [
            new Outcome(S["Scheduled"]),
            new Outcome(S["Exhausted"]),
            new Outcome(S["Failed"]),
        ];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var activityItemId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(ActivityItemId), workflowContext, null))?.Trim();
        int? delayMinutes = null;

        if (!string.IsNullOrWhiteSpace(DelayMinutes))
        {
            var delayText = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(DelayMinutes), workflowContext, null))?.Trim();

            if (!string.IsNullOrEmpty(delayText))
            {
                if (!int.TryParse(delayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) || minutes < 0)
                {
                    _logger.LogWarning(
                        "The Schedule Dialer Retry task resolved a delay of '{Delay}', which is not a whole number of minutes.",
                        delayText.SanitizeLogValue());

                    return WorkflowOutcomeResults.From("Failed");
                }

                delayMinutes = minutes;
            }
        }

        try
        {
            var result = await _retryScheduler.ScheduleRetryAsync(activityItemId, delayMinutes);

            if (result.Status == DialerRetryScheduleStatus.Scheduled)
            {
                workflowContext.Output["NextActivityItemId"] = result.NextActivityItemId;
                workflowContext.Output["NextAttemptNumber"] = result.AttemptNumber;
                workflowContext.Output["NextAttemptDueUtc"] = result.DueUtc;

                return WorkflowOutcomeResults.From("Scheduled");
            }

            if (result.Status == DialerRetryScheduleStatus.AttemptsExhausted)
            {
                return WorkflowOutcomeResults.From("Exhausted");
            }

            _logger.LogWarning(
                "The Schedule Dialer Retry task could not try activity '{ActivityItemId}' again: {Reason}",
                activityItemId.SanitizeLogValue(),
                result.Reason.SanitizeLogValue());

            return WorkflowOutcomeResults.From("Failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while scheduling the next dialer attempt of activity '{ActivityItemId}'.", activityItemId.SanitizeLogValue());

            return WorkflowOutcomeResults.From("Failed");
        }
    }
}
