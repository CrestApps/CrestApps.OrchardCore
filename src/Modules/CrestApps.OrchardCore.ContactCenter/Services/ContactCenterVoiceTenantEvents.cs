using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Runs a provider-truth reconciliation pass when the tenant activates so short restarts do not leave
/// persisted voice offers out of sync with the telephony server.
/// </summary>
public sealed class ContactCenterVoiceTenantEvents : ModularTenantEvents
{
    private readonly IProviderCallStateSynchronizationService _synchronizationService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceTenantEvents"/> class.
    /// </summary>
    /// <param name="synchronizationService">The provider call-state synchronization service.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterVoiceTenantEvents(
        IProviderCallStateSynchronizationService synchronizationService,
        ILogger<ContactCenterVoiceTenantEvents> logger)
    {
        _synchronizationService = synchronizationService;
        _logger = logger;
    }

    /// <summary>
    /// Reconciles active voice interactions during tenant activation.
    /// </summary>
    public override async Task ActivatingAsync()
    {
        try
        {
            await _synchronizationService.ReconcileActiveInteractionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while reconciling Contact Center voice state during tenant activation.");
        }
    }
}
