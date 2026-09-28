using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Finishes a transfer a phone menu started to an outside number, once the provider says whether the destination
/// answered: an answered transfer settles the call as transferred, and a failed one puts the caller where the menu's
/// fallback, or else the entry point, sends them.
/// </summary>
/// <remarks>
/// A concurrency conflict is never caught here: the webhook inbox retries the delivery in a fresh scope, which reads
/// the interaction again and finds the transfer either still waiting or already dealt with.
/// </remarks>
public sealed class IvrExternalTransferOutcomeSink : IExternalTransferOutcomeSink
{
    private readonly IInteractionManager _interactionManager;
    private readonly IIvrExternalTransferService _transferService;
    private readonly IIvrCallRouter _callRouter;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IvrExternalTransferOutcomeSink"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="transferService">The service that started the transfer.</param>
    /// <param name="callRouter">The router that puts a caller whose transfer failed somewhere else.</param>
    /// <param name="logger">The logger.</param>
    public IvrExternalTransferOutcomeSink(
        IInteractionManager interactionManager,
        IIvrExternalTransferService transferService,
        IIvrCallRouter callRouter,
        ILogger<IvrExternalTransferOutcomeSink> logger)
    {
        _interactionManager = interactionManager;
        _transferService = transferService;
        _callRouter = callRouter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleAsync(ExternalTransferOutcome outcome, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (string.IsNullOrEmpty(outcome.InteractionId))
        {
            return false;
        }

        var interaction = await _interactionManager.FindByIdAsync(outcome.InteractionId, cancellationToken);

        // A caller who has already gone, or a transfer already answered or failed, has nothing waiting on this: the
        // destination hanging up after it answered is the end of an ordinary transferred call.
        if (interaction is null || interaction.IsSettled)
        {
            return false;
        }

        if (outcome.Answered)
        {
            return await _transferService.CompleteAsync(interaction, cancellationToken);
        }

        var failedDestinationId = await _transferService.FailAsync(interaction, outcome.HangupCause, outcome.CallerLeft, cancellationToken);

        if (failedDestinationId is null)
        {
            return false;
        }

        // The caller hung up while the destination rang; their own hang-up ends the call.
        if (outcome.CallerLeft)
        {
            return true;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "The external transfer of interaction '{InteractionId}' failed ({Cause}); the caller is being put through to the menu's fallback.",
                interaction.ItemId.SanitizeLogValue(),
                outcome.HangupCause.SanitizeLogValue());
        }

        await _callRouter.RecoverFailedTransferAsync(interaction.ItemId, failedDestinationId, cancellationToken);

        return true;
    }
}
