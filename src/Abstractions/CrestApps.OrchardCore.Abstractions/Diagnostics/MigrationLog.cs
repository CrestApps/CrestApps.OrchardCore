using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// The log messages of the deferred data migrations, with event ids 20000 to 20009 of
/// <see cref="CrestAppsEventIds.Startup"/>.
/// </summary>
internal static partial class MigrationLog
{
    /// <summary>
    /// A deferred data migration started.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Startup, Level = LogLevel.Information, Message = "The data migration '{Migration}' started.")]
    public static partial void Started(ILogger logger, string migration);

    /// <summary>
    /// A deferred data migration completed.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Startup + 1, Level = LogLevel.Information, Message = "The data migration '{Migration}' completed in {ElapsedMilliseconds} ms.")]
    public static partial void Completed(ILogger logger, string migration, long elapsedMilliseconds);

    /// <summary>
    /// A deferred data migration failed.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Startup + 2, Level = LogLevel.Error, Message = "The data migration '{Migration}' failed after {ElapsedMilliseconds} ms.")]
    public static partial void Failed(ILogger logger, Exception exception, string migration, long elapsedMilliseconds);
}
