using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Geo.Core.Telemetry;

/// <summary>
/// The log messages of the geo services, with event ids 13300 to 13399 of <see cref="CrestAppsEventIds.Scheduling"/>.
/// </summary>
internal static partial class GeoLog
{
    /// <summary>
    /// The pending geocoding requests could not be counted for the queue depth metric. The queue run is not affected.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Scheduling + 300, Level = LogLevel.Debug, Message = "The pending geocoding requests could not be counted for the queue depth metric.")]
    public static partial void QueueDepthUnavailable(ILogger logger, Exception exception);
}
