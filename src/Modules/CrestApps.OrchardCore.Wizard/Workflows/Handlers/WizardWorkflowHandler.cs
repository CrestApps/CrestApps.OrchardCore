using CrestApps.OrchardCore.Wizard.Handlers;
using CrestApps.OrchardCore.Wizard.Workflows.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Wizard.Workflows.Handlers;

/// <summary>
/// Bridges the wizard lifecycle to Orchard workflow events so that authors can react to wizard activity
/// using workflows.
/// </summary>
public sealed class WizardWorkflowHandler : WizardHandlerBase
{
    private readonly IWorkflowManager _workflowManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardWorkflowHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager used to trigger workflow events.</param>
    /// <param name="logger">The logger instance for this handler.</param>
    public WizardWorkflowHandler(
        IWorkflowManager workflowManager,
        ILogger<WizardWorkflowHandler> logger)
    {
        _workflowManager = workflowManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task ActivatedAsync(WizardFlowActivatedContext context)
        => TriggerAsync(WizardStartedEvent.EventName, context.Flow);

    /// <inheritdoc/>
    public override Task LoadedAsync(WizardFlowLoadedContext context)
        => TriggerAsync(WizardStepDisplayedEvent.EventName, context.Flow);

    /// <inheritdoc/>
    public override Task CompletedAsync(WizardFlowCompletedContext context)
        => TriggerAsync(WizardCompletedEvent.EventName, context.Flow);

    /// <inheritdoc/>
    public override Task FailedAsync(WizardFlowFailedContext context)
        => TriggerAsync(WizardFailedEvent.EventName, context.Flow);

    private async Task TriggerAsync(string eventName, WizardFlow flow)
    {
        var session = flow.Session;

        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", session.SessionId },
                { "WizardType", session.WizardType },
                { "DefinitionId", (session as WizardSession)?.DefinitionId },
                { "CurrentStep", flow.GetCurrentStep()?.Key },
                { "Status", session.Status.ToString() },
                { "Session", session },
            };

            await _workflowManager.TriggerEventAsync(
                eventName,
                input,
                correlationId: session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger the '{EventName}' workflow event for wizard session '{SessionId}'.", eventName, session.SessionId);
        }
    }
}
