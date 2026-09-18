using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Periodically reconciles in-progress telephony interactions with provider-authoritative state.
/// </summary>
public sealed class TelephonyInteractionReconciliationCycle : ITelephonyInteractionReconciliationCycle
{
    private readonly ITelephonyInteractionSynchronizationService _synchronizationService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyInteractionReconciliationCycle"/> class.
    /// </summary>
    /// <param name="synchronizationService">The synchronization service.</param>
    /// <param name="logger">The logger.</param>
    public TelephonyInteractionReconciliationCycle(
        ITelephonyInteractionSynchronizationService synchronizationService,
        ILogger<TelephonyInteractionReconciliationCycle> logger)
    {
        _synchronizationService = synchronizationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _synchronizationService.ReconcileActiveInteractionsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The tenant is shutting down; stop quietly instead of logging the cancellation as a reconciliation failure.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while reconciling telephony interaction state.");
        }
    }
}
