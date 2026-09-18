using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Periodically fans out queued SMS broadcasts. Each recipient becomes an individual 1:1 thread; progress is
/// persisted after every recipient so a restart resumes without re-sending. The sweep is a no-op when there
/// are no queued or in-progress broadcasts.
/// </summary>
public sealed class SmsBroadcastCycle : ISmsBroadcastCycle
{
    private readonly ISmsBroadcastService _broadcastService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsBroadcastCycle"/> class.
    /// </summary>
    /// <param name="broadcastService">The broadcast service.</param>
    /// <param name="logger">The logger.</param>
    public SmsBroadcastCycle(
        ISmsBroadcastService broadcastService,
        ILogger<SmsBroadcastCycle> logger)
    {
        _broadcastService = broadcastService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _broadcastService.ProcessPendingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing SMS broadcasts.");
        }
    }
}
