using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Subscriptions.Core.Telemetry;

/// <summary>
/// The source-generated log messages of subscriptions, with event ids from 12200 to 12299.
/// </summary>
internal static partial class SubscriptionsLog
{
    /// <summary>
    /// Logs that a site a customer paid for was created.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="jobId">The id of the provisioning job.</param>
    /// <param name="attempt">The attempt that created the site.</param>
    /// <param name="elapsedSeconds">How long the attempt took, in seconds.</param>
    [LoggerMessage(
        EventId = CrestAppsEventIds.Commerce + 200,
        Level = LogLevel.Information,
        Message = "Provisioning job '{JobId}' created its site on attempt {Attempt} in {ElapsedSeconds:F1} seconds.")]
    public static partial void TenantProvisioned(ILogger logger, string jobId, int attempt, double elapsedSeconds);
}
