using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Models;

/// <summary>
/// Transfers a phone call the AI is holding to a phone number: cold (connect and drop off), warm (ring the number
/// while the caller holds, brief whoever answers, then connect) or screened (ask whoever answers whether they can
/// take it, and give the caller back to the AI when they cannot).
/// </summary>
public sealed class TransferAICallTask : TaskActivity<TransferAICallTask>
{
    private readonly IEnumerable<IAIVoiceCallTransfers> _transfers;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferAICallTask"/> class.
    /// </summary>
    /// <param name="transfers">The AI call transfers, when AI voice is on.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransferAICallTask(
        IEnumerable<IAIVoiceCallTransfers> transfers,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<TransferAICallTask> logger,
        IStringLocalizer<TransferAICallTask> stringLocalizer)
    {
        _transfers = transfers;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Transfer AI Call"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["AI Conversations"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the call's activity.
    /// </summary>
    public string ActivityId
    {
        get => GetProperty(() => "{{ Workflow.Input.ActivityId }}");
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression of the number to ring, with its country code.
    /// </summary>
    public string PhoneNumber
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets how the call is transferred.
    /// </summary>
    public AICallTransferMode Mode
    {
        get => GetProperty(() => AICallTransferMode.Warm);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid template of what whoever answers is told. Empty has the AI write it.
    /// </summary>
    public string Briefing
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid template of the line the caller hears once connected. Empty says nothing.
    /// </summary>
    public string Introduction
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets how long the number rings before the transfer is given up.
    /// </summary>
    public int RingTimeoutSeconds
    {
        get => GetProperty(() => 30);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the audio the caller hears while the number rings: a URL, or the name of audio the provider holds.
    /// </summary>
    public string HoldAudio
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the person rung sees the caller's number, where the carrier allows it.
    /// </summary>
    public bool ShowCallerNumber
    {
        get => GetProperty(() => false);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var transfers = _transfers.FirstOrDefault();
        var activityId = await EvaluateAsync(ActivityId, workflowContext);
        var phoneNumber = await EvaluateAsync(PhoneNumber, workflowContext);

        if (transfers is null || string.IsNullOrEmpty(activityId) || string.IsNullOrEmpty(phoneNumber))
        {
            _logger.LogWarning("The Transfer AI Call task needs AI voice to be on, a call's activity and a phone number.");
            workflowContext.LastResult = "The call or the phone number is missing.";

            return Outcome("Failed");
        }

        var result = await transfers.TransferAsync(activityId, new AITakeOverRequest
        {
            TargetPhoneNumber = phoneNumber,
            TransferMode = Mode,
            Briefing = await EvaluateAsync(Briefing, workflowContext),
            Introduction = await EvaluateAsync(Introduction, workflowContext),
            RingTimeoutSeconds = RingTimeoutSeconds,
            HoldAudio = await EvaluateAsync(HoldAudio, workflowContext),
            ShowCallerNumber = ShowCallerNumber,
            RequestedBy = "Workflow",
        });

        workflowContext.LastResult = result.Message;

        if (!result.Succeeded && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The Transfer AI Call task could not transfer activity '{ActivityId}': {Message}",
                activityId.SanitizeLogValue(),
                result.Message.SanitizeLogValue());
        }

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();
}
