using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Participates in the Contact Center feature lifecycle for the Voice feature. On feature disable it
/// quiesces and drains feature-owned voice work; on tenant activation it reopens work admission. The
/// provider-truth reconciliation pass itself is owned by <c>ProviderCallStateReconciliationBackgroundTask</c>,
/// which invokes <see cref="ReconcileProviderStateAsync"/> under the work-admission gate, so tenant
/// activation stays free of provider and database work.
/// </summary>
internal sealed class ContactCenterVoiceLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
{
    private readonly IProviderCallStateSynchronizationService _synchronizationService;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly TimeSpan _drainTimeout;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceLifecycleParticipant"/> class.
    /// </summary>
    /// <param name="synchronizationService">The provider call-state synchronization service.</param>
    /// <param name="workManager">The feature work manager.</param>
    /// <param name="options">The feature lifecycle options.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterVoiceLifecycleParticipant(
        IProviderCallStateSynchronizationService synchronizationService,
        IContactCenterFeatureWorkManager workManager,
        IOptions<ContactCenterFeatureLifecycleOptions> options,
        ILogger<ContactCenterVoiceLifecycleParticipant> logger)
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
    /// Reopens voice work admission when a fresh tenant shell activates. Provider-truth reconciliation is
    /// intentionally not performed here: it runs under the work-admission gate from the scheduled
    /// reconciliation background task, so tenant activation stays free of provider and database work.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        _workManager.Activate(FeatureId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reconciles active voice interactions against current provider call state. Invoked by the scheduled
    /// reconciliation background task after it has acquired the feature work-admission lease.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
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
            _logger.LogError(ex, "An error occurred while reconciling Contact Center voice provider state.");
        }
    }
}
