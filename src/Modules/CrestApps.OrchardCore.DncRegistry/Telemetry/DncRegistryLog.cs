using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.DncRegistry.Telemetry;

/// <summary>
/// The log messages of the do-not-call registry module, with their event ids. They never carry a phone number.
/// </summary>
internal static partial class DncRegistryLog
{
    private const int FirstEventId = CrestAppsEventIds.ContentTransfer + 400;

    /// <summary>
    /// Logs how one run of the import of a local list ended.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId,
        Level = LogLevel.Information,
        Message = "The import run of local DNC list '{ListId}' ended as {Outcome} after {ElapsedSeconds} s: {ImportedRows} rows saved and {RejectedRows} rows rejected in this run.")]
    public static partial void ImportRunEnded(ILogger logger, string listId, string outcome, double elapsedSeconds, long importedRows, long rejectedRows);
}
