using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Inventory.Core.Telemetry;

/// <summary>
/// The log messages of inventory. Their event ids start at <see cref="CrestAppsEventIds.ContentTransfer"/> + 800.
/// </summary>
internal static partial class InventoryLog
{
    /// <summary>
    /// Logs what a stock count import did.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="counted">The number of items that got a new count.</param>
    /// <param name="unchanged">The number of items that already had the count given.</param>
    /// <param name="notApplied">The number of rows that were not applied.</param>
    /// <param name="elapsedMilliseconds">How long the import took, in milliseconds.</param>
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 800,
        Level = LogLevel.Information,
        Message = "A stock count was applied in {ElapsedMilliseconds} ms: {Counted} items got a new count, {Unchanged} already had it and {NotApplied} rows were not applied.")]
    public static partial void StockCountApplied(ILogger logger, int counted, int unchanged, int notApplied, double elapsedMilliseconds);

    /// <summary>
    /// Logs that a stock count import stopped at its row limit.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="line">The line the import stopped at.</param>
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 801,
        Level = LogLevel.Warning,
        Message = "A stock count stopped at line {Line} because the file has more rows than are read at once. The rows after it were not applied.")]
    public static partial void StockCountTruncated(ILogger logger, int line);
}
