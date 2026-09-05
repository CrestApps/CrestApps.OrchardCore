using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Turns a key press on an entry-point menu into the caller actually being put somewhere.
/// </summary>
public sealed class InboundVoiceDigitsSink : IInboundVoiceDigitsSink
{
    private readonly IInteractionManager _interactionManager;
    private readonly IEntryPointFlowResolver _flowResolver;
    private readonly IIvrExecutionService _ivrExecutionService;
    private readonly IActivityQueueService _queueService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundVoiceDigitsSink"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="flowResolver">The lookup from a live call to the menu it is in.</param>
    /// <param name="ivrExecutionService">The menu runtime.</param>
    /// <param name="queueService">The queue service used to put the caller in line.</param>
    /// <param name="logger">The logger.</param>
    public InboundVoiceDigitsSink(
        IInteractionManager interactionManager,
        IEntryPointFlowResolver flowResolver,
        IIvrExecutionService ivrExecutionService,
        IActivityQueueService queueService,
        ILogger<InboundVoiceDigitsSink> logger)
    {
        _interactionManager = interactionManager;
        _flowResolver = flowResolver;
        _ivrExecutionService = ivrExecutionService;
        _queueService = queueService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleDigitsAsync(InboundVoiceDigitsEvent digitsEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(digitsEvent);

        if (string.IsNullOrEmpty(digitsEvent.ProviderCallId))
        {
            return false;
        }

        var interaction = await _interactionManager.FindByProviderInteractionIdAsync(
            digitsEvent.ProviderName,
            digitsEvent.ProviderCallId,
            cancellationToken);

        // Another feature on this tenant may collect digits for its own reasons. Claiming a press that belongs to
        // nothing here would swallow their event.
        if (interaction is null)
        {
            return false;
        }

        var flow = await _flowResolver.FindFlowAsync(interaction, cancellationToken);

        if (flow is null)
        {
            return false;
        }

        // A caller who pressed nothing before the menu timed out has still made a move: the flow is what decides
        // whether that repeats the menu or sends them to the fallback, and dropping it leaves them in silence.
        var step = await _ivrExecutionService.HandleDigitsAsync(
            interaction,
            flow,
            digitsEvent.Digits,
            digitsEvent.DeliveryId,
            cancellationToken);

        switch (step.Kind)
        {
            case IvrStepKind.RouteToQueue when !string.IsNullOrEmpty(step.TargetId) && !string.IsNullOrEmpty(interaction.ActivityItemId):
                await _queueService.EnqueueAsync(interaction.ActivityItemId, step.TargetId, priority: null, cancellationToken);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "A caller chose queue '{QueueId}' from the entry-point menu on interaction '{InteractionId}'.",
                        step.TargetId.SanitizeLogValue(),
                        interaction.ItemId.SanitizeLogValue());
                }

                return true;

            default:
                // A prompt means the caller is still choosing, and a sub-menu is not a destination: queueing them
                // here would put them in line while they are still being asked where they want to go. The other
                // outcomes are settled by the flow itself.
                return true;
        }
    }
}
