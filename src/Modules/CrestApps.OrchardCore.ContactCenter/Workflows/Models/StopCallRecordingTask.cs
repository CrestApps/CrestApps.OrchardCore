using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// A workflow task activity that stops recording for a Contact Center interaction. Because recording is a
/// release-critical mutation, an interrupted command yields the distinct <c>Indeterminate</c> outcome.
/// </summary>
public sealed class StopCallRecordingTask : TaskActivity<StopCallRecordingTask>
{
    private readonly IContactCenterRecordingService _recordingService;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StopCallRecordingTask"/> class.
    /// </summary>
    /// <param name="recordingService">The recording service used to stop recording.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public StopCallRecordingTask(
        IContactCenterRecordingService recordingService,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<StopCallRecordingTask> logger,
        IStringLocalizer<StopCallRecordingTask> stringLocalizer)
    {
        _recordingService = recordingService;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Stop Call Recording"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the interaction identifier to stop recording.
    /// </summary>
    public string InteractionId
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
            new Outcome(S["Indeterminate"]),
            new Outcome(S["Failed"]),
        ];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var interactionId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(InteractionId), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(interactionId))
        {
            _logger.LogWarning("The Stop Call Recording task resolved an empty interaction identifier.");

            return WorkflowOutcomeResults.From("Failed");
        }

        try
        {
            var result = await _recordingService.StopAsync(interactionId);

            if (result.Succeeded)
            {
                return WorkflowOutcomeResults.From("Done");
            }

            return result.OutcomeUnknown
                ? WorkflowOutcomeResults.From("Indeterminate")
                : WorkflowOutcomeResults.From("Failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while stopping recording for interaction '{InteractionId}'.", interactionId.SanitizeLogValue());

            return WorkflowOutcomeResults.From("Failed");
        }
    }
}
