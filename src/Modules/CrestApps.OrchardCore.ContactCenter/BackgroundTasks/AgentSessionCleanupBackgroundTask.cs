using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Signs out agents whose real-time session heartbeat has gone stale so routing stops targeting a client
/// that is no longer connected. Acts as the safety net behind the SignalR disconnect handler.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Agent Session Cleanup",
    Schedule = "* * * * *",
    Description = "Expires agent sessions whose heartbeat has gone stale and signs the agent out.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class AgentSessionCleanupBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var sessionService = serviceProvider.GetRequiredService<IAgentSessionService>();
        var logger = serviceProvider.GetRequiredService<ILogger<AgentSessionCleanupBackgroundTask>>();

        try
        {
            var expired = await sessionService.ExpireStaleAsync(cancellationToken);

            if (expired > 0 && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Expired {Count} stale Contact Center agent session(s).", expired);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while expiring stale Contact Center agent sessions.");
        }
    }
}
