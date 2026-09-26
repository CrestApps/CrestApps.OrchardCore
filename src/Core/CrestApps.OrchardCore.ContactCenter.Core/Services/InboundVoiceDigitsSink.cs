using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Turns a key press on an entry-point menu into the caller actually being put somewhere.
/// </summary>
/// <remarks>
/// A concurrency conflict is never caught here. Two deliveries of one key press, or a key press racing the provider's
/// other events for the call, conflict on the interaction; the webhook inbox retries the loser in a fresh scope, which
/// reads the caller's committed position and recognises the delivery. Settling the conflict here would drop the key
/// press and leave the caller in silence.
/// </remarks>
public sealed class InboundVoiceDigitsSink : IInboundVoiceDigitsSink
{
    private readonly IInteractionManager _interactionManager;
    private readonly IEntryPointFlowResolver _flowResolver;
    private readonly IIvrExecutionService _ivrExecutionService;
    private readonly IIvrCallRouter _callRouter;
    private readonly IQueueCallbackOfferResponder _callbackOffers;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundVoiceDigitsSink"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="flowResolver">The lookup from a live call to the menu it is in.</param>
    /// <param name="ivrExecutionService">The menu runtime.</param>
    /// <param name="callRouter">The router that puts the caller where the menu decided.</param>
    /// <param name="callbackOffers">What acts on a waiting caller's answer to the queue's callback offer.</param>
    /// <param name="auditRecorder">The recorder a caller who hangs up in the menu is written to.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public InboundVoiceDigitsSink(
        IInteractionManager interactionManager,
        IEntryPointFlowResolver flowResolver,
        IIvrExecutionService ivrExecutionService,
        IIvrCallRouter callRouter,
        IQueueCallbackOfferResponder callbackOffers,
        IContactCenterAuditRecorder auditRecorder,
        IClock clock,
        ILogger<InboundVoiceDigitsSink> logger)
    {
        _interactionManager = interactionManager;
        _flowResolver = flowResolver;
        _ivrExecutionService = ivrExecutionService;
        _callRouter = callRouter;
        _callbackOffers = callbackOffers;
        _auditRecorder = auditRecorder;
        _clock = clock;
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

        var entryPoint = await _flowResolver.FindEntryPointAsync(interaction, cancellationToken);
        var flow = entryPoint?.IvrFlow;

        // A caller who has left the menu, or never had one, is somewhere else now: a queue's callback offer collects a
        // key on the same call, and treating it as a menu choice would move somebody already waiting for an agent.
        // It is the offer's answer, and was dropped here until the offer had somebody to act on it.
        if (flow is null || IvrExecutionService.ReadState(interaction).Completed)
        {
            return await _callbackOffers.HandleAsync(interaction, digitsEvent, cancellationToken);
        }

        switch (digitsEvent.Outcome)
        {
            case InboundVoiceDigitsOutcome.Cancelled:
                // The platform replaced the menu with something else, such as a newer prompt; that command owns
                // what the caller hears next.
                return true;

            case InboundVoiceDigitsOutcome.CallerHungUp:
                await RecordHungUpAsync(interaction, cancellationToken);

                return true;
        }

        // A caller who pressed nothing before the menu timed out has still made a move: the flow is what decides
        // whether that repeats the menu or sends them to the fallback, and dropping it leaves them in silence.
        var digits = digitsEvent.Outcome == InboundVoiceDigitsOutcome.TimedOut ? null : digitsEvent.Digits;
        var step = await _ivrExecutionService.HandleDigitsAsync(
            interaction,
            flow,
            digits,
            digitsEvent.DeliveryId,
            cancellationToken);

        await _callRouter.RouteAsync(interaction.ItemId, entryPoint, step, cancellationToken);

        return true;
    }

    // A caller who gives up in the menu abandoned the call. The platform answered them to play it, which the reports
    // would otherwise read as a call somebody answered.
    private async Task RecordHungUpAsync(Interaction interaction, CancellationToken cancellationToken)
    {
        await _ivrExecutionService.EndAsync(interaction, "CallerHungUp", cancellationToken);

        var data = ContactCenterCallAudit.ForInteraction(interaction);
        data.Reason = CallLifecycleReasons.CallerHungUp;
        data.Details["nodeId"] = IvrExecutionService.ReadState(interaction).CurrentNodeId ?? string.Empty;
        data.Details["stage"] = "ivr";

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.CallAbandoned,
            data,
            _clock.UtcNow,
            new ContactCenterActor(ContactCenterActorType.Customer),
            $"call-abandoned:{interaction.ItemId}",
            cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The caller on interaction '{InteractionId}' hung up in the entry-point menu.",
                interaction.ItemId.SanitizeLogValue());
        }
    }
}
