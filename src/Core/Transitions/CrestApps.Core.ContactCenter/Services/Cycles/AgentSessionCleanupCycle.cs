using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Signs out agents whose real-time session heartbeat has gone stale so routing stops targeting a client
/// that is no longer connected. Acts as the safety net behind the SignalR disconnect handler.
/// </summary>
public sealed class AgentSessionCleanupCycle : IAgentSessionCleanupCycle
{
    private readonly IAgentSessionService _sessionService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionCleanupCycle"/> class.
    /// </summary>
    /// <param name="sessionService">The session service.</param>
    /// <param name="logger">The logger.</param>
    public AgentSessionCleanupCycle(
        IAgentSessionService sessionService,
        ILogger<AgentSessionCleanupCycle> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var expired = await _sessionService.ExpireStaleAsync(cancellationToken);

            if (expired > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Expired {Count} stale Contact Center agent session(s).", expired);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while expiring stale Contact Center agent sessions.");
        }
    }
}
