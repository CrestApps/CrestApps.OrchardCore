using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Telemetry;

/// <summary>
/// The log messages of the AI core services, with event ids 11000 to 11099 of <see cref="CrestAppsEventIds.AI"/>.
/// </summary>
internal static partial class AICoreLog
{
    /// <summary>
    /// A tool did not answer within its time limit and was stopped.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.AI + 1, Level = LogLevel.Warning, Message = "The AI tool '{ToolName}' did not answer within {TimeoutSeconds} seconds and was stopped.")]
    public static partial void ToolTimedOut(ILogger logger, string toolName, double timeoutSeconds);

    /// <summary>
    /// A data source sync finished.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.AI + 2, Level = LogLevel.Information, Message = "Synced {DocumentCount} chunks of {SourceDocumentCount} documents for data source '{DataSourceId}' to master index '{IndexName}' in {ElapsedMilliseconds} ms; {FailedCount} documents failed.")]
    public static partial void DataSourceSynced(ILogger logger, int documentCount, int sourceDocumentCount, string dataSourceId, string indexName, long elapsedMilliseconds, int failedCount);

    /// <summary>
    /// A data source sync failed and is tried again.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.AI + 3, Level = LogLevel.Warning, Message = "Error syncing data source '{DataSourceId}' (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay} seconds.")]
    public static partial void DataSourceSyncRetrying(ILogger logger, Exception exception, string dataSourceId, int attempt, int maxAttempts, double delay);

    /// <summary>
    /// A recipe an AI tool imported failed when it ran after the request.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.AI + 4, Level = LogLevel.Warning, Message = "The recipe an AI tool imported could not be executed after the request ended.")]
    public static partial void DeferredRecipeImportFailed(ILogger logger);
}
