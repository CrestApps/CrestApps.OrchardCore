using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Runs a provider-truth reconciliation pass when the tenant activates so short restarts do not leave
/// persisted voice offers out of sync with the telephony server.
/// </summary>
internal sealed class ContactCenterVoiceTenantEvents : IContactCenterFeatureLifecycleParticipant
{
    private readonly IProviderCallStateSynchronizationService _synchronizationService;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceTenantEvents"/> class.
    /// </summary>
    /// <param name="synchronizationService">The provider call-state synchronization service.</param>
    /// <param name="workManager">The feature work manager.</param>
    /// <param name="options">The feature lifecycle options.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterVoiceTenantEvents(
        IProviderCallStateSynchronizationService synchronizationService,
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options,
        ILogger<ContactCenterVoiceTenantEvents> logger)
    {
        _synchronizationService = synchronizationService;
        _workManager = workManager;
        _drainTimeout = TimeSpan.FromSeconds(options.Value.DrainTimeoutSeconds);
        _logger = logger;
    }

    /// <inheritdoc/>
    public string FeatureId => ContactCenterConstants.Feature.Voice;

    /// <inheritdoc/>
    public Task QuiesceAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Quiesce(FeatureId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DrainAsync(CancellationToken cancellationToken = default)
    {
        return _workManager.DrainAsync(FeatureId, _drainTimeout, cancellationToken);
    }

    /// <summary>
    /// Reconciles active voice interactions when a fresh tenant shell activates.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Activate(FeatureId);
        await ReconcileProviderStateAsync(cancellationToken);
    }

    public async Task ReconcileProviderStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _synchronizationService.ReconcileActiveInteractionsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while reconciling Contact Center voice state during tenant activation.");
        }
    }
}
