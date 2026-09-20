using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Revalidates active provider-backed interactions against the telephony server so restarts and missed
/// live events do not leave queued voice work out of sync.
/// </summary>
public sealed class ProviderCallStateReconciliationCycle : IProviderCallStateReconciliationCycle
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IVoiceLifecycleReconciler _participant;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCallStateReconciliationCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="participant">The participant.</param>
    /// <param name="logger">The logger.</param>
    public ProviderCallStateReconciliationCycle(
        IContactCenterFeatureWorkManager workManager,
        IVoiceLifecycleReconciler participant,
        ILogger<ProviderCallStateReconciliationCycle> logger)
    {
        _workManager = workManager;
        _participant = participant;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterCapabilities.Voice);

        if (workLease is null)
        {
            return;
        }


        try
        {
            await _participant.ReconcileProviderStateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while reconciling Contact Center provider call state.");
        }
    }
}
