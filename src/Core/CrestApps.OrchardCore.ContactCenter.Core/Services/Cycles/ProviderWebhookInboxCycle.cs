using CrestApps.Core.ContactCenter;
using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Retries provider webhook inbox messages whose immediate persisted dispatch did not complete. Each due
/// message is isolated in its own fresh child scope so a poison delivery never blocks the rest of the batch.
/// </summary>
public sealed class ProviderWebhookInboxCycle : IProviderWebhookInboxCycle
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IProviderWebhookInbox _inbox;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookInboxCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="inbox">The inbox.</param>
    /// <param name="logger">The logger.</param>
    public ProviderWebhookInboxCycle(
        IContactCenterFeatureWorkManager workManager,
        IProviderWebhookInbox inbox,
        ILogger<ProviderWebhookInboxCycle> logger)
    {
        _workManager = workManager;
        _inbox = inbox;
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
            var processed = await _inbox.DispatchDueAsync(cancellationToken);

            if (processed > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Processed {Count} provider webhook _inbox message(s).", processed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "An error occurred while dispatching the provider webhook _inbox.");
        }
    }
}
