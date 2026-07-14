using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Reconciles provider-authoritative telephony state when a tenant starts.
/// </summary>
public sealed class TelephonyInteractionTenantEvents : ModularTenantEvents
{
    private readonly ITelephonyInteractionSynchronizationService _synchronizationService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyInteractionTenantEvents"/> class.
    /// </summary>
    /// <param name="synchronizationService">The telephony interaction synchronization service.</param>
    /// <param name="logger">The logger.</param>
    public TelephonyInteractionTenantEvents(
        ITelephonyInteractionSynchronizationService synchronizationService,
        ILogger<TelephonyInteractionTenantEvents> logger)
    {
        _synchronizationService = synchronizationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync()
    {
        try
        {
            await _synchronizationService.ReconcileActiveInteractionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while reconciling telephony interaction state during tenant activation.");
        }
    }
}
