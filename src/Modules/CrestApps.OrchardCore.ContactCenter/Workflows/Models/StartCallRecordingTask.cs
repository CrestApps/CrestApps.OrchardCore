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
/// A workflow task activity that starts recording for a Contact Center interaction. Because recording is a
/// release-critical mutation, an interrupted command yields the distinct <c>Indeterminate</c> outcome.
/// </summary>
public sealed class StartCallRecordingTask : TaskActivity<StartCallRecordingTask>
{
    private readonly IContactCenterRecordingService _recordingService;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartCallRecordingTask"/> class.
    /// </summary>
    /// <param name="recordingService">The recording service used to start recording.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator used to resolve Liquid fields.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="stringLocalizer">The string localizer for this task.</param>
    public StartCallRecordingTask(
        IContactCenterRecordingService recordingService,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<StartCallRecordingTask> logger,
        IStringLocalizer<StartCallRecordingTask> stringLocalizer)
    {
        _recordingService = recordingService;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Start Call Recording"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Contact Center"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the interaction identifier to record.
    /// </summary>
    public string InteractionId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcomes(S["Done"], S["Indeterminate"], S["Failed"]);
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var interactionId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(InteractionId), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(interactionId))
        {
            _logger.LogWarning("The Start Call Recording task resolved an empty interaction identifier.");

            return Outcomes("Failed");
        }

        try
        {
            var result = await _recordingService.StartAsync(interactionId);

            if (result.Succeeded)
            {
                return Outcomes("Done");
            }

            return result.OutcomeUnknown ? Outcomes("Indeterminate") : Outcomes("Failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while starting recording for interaction '{InteractionId}'.", interactionId.SanitizeLogValue());

            return Outcomes("Failed");
        }
    }
}
