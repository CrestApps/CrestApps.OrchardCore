using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Redelivers Contact Center domain events whose handler dispatch previously failed. It is the durable
/// retry mechanism behind <see cref="IContactCenterOutbox"/>, so a transient handler failure no longer
/// silently drops an event. Each due message is isolated in its own fresh child scope so a poison message
/// never blocks the rest of the batch.
/// </summary>
public sealed class OutboxDispatchCycle : IOutboxDispatchCycle
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IContactCenterOutbox _outbox;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxDispatchCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="outbox">The outbox.</param>
    /// <param name="logger">The logger.</param>
    public OutboxDispatchCycle(
        IContactCenterFeatureWorkManager workManager,
        IContactCenterOutbox outbox,
        ILogger<OutboxDispatchCycle> logger)
    {
        _workManager = workManager;
        _outbox = outbox;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterCapabilities.Core);

        if (workLease is null)
        {
            return;
        }


        try
        {
            var redelivered = await _outbox.DispatchDueAsync(cancellationToken);

            if (redelivered > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Redelivered {Count} Contact Center event(s) from the _outbox.", redelivered);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while dispatching the Contact Center event _outbox.");
        }
    }
}
