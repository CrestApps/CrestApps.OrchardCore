using System.Diagnostics;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

/// <summary>
/// Tallies what became of the rows one import run reads, and records the tally on the Content Transfer instruments
/// only once the run saves its progress. A batch whose save fails, or that a paused import or an abandoned run never
/// saves, is read again by the next run, so counting its rows before the save would count them twice.
/// </summary>
internal sealed class ImportRunTelemetry
{
    /// <summary>
    /// How often the run logs its progress.
    /// </summary>
    internal static readonly TimeSpan ProgressLogInterval = TimeSpan.FromMinutes(1);

    private const int RowExceptionsLoggedInFull = 5;
    private const int RowExceptionLogInterval = 100;

    private readonly Dictionary<string, long> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _skipped = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _errors = new(StringComparer.Ordinal);
    private readonly ILogger _logger;
    private readonly string _entryId;
    private readonly long _startTimestamp;
    private readonly int _firstRowOfRun;

    private long _lastProgressLogTimestamp;
    private int _rowExceptions;

    public ImportRunTelemetry(
        ILogger logger,
        string entryId,
        int firstRowOfRun)
    {
        _logger = logger;
        _entryId = entryId;
        _firstRowOfRun = firstRowOfRun;
        _startTimestamp = Stopwatch.GetTimestamp();
        _lastProgressLogTimestamp = _startTimestamp;
    }

    /// <summary>
    /// Gets the number of rows tallied since the progress was last saved.
    /// </summary>
    public long PendingRows { get; private set; }

    /// <summary>
    /// Tallies a row, such as an imported or an empty one.
    /// </summary>
    /// <param name="result">What became of the row, such as <c>imported</c> or <c>empty</c>.</param>
    public void AddRow(string result)
    {
        Increment(_rows, result);
        PendingRows++;
    }

    /// <summary>
    /// Tallies a row a row filter skipped, by the bounded code the filter set.
    /// </summary>
    /// <param name="skipReasonCode">The code the filter set in <see cref="ContentImportRowFilterContext.SkipReasonCode"/>.</param>
    public void AddSkipped(string skipReasonCode)
    {
        AddRow(ContentTransferDiagnostics.ResultSkippedByFilter);
        Increment(_skipped, ContentTransferDiagnostics.NormalizeCode(skipReasonCode));
    }

    /// <summary>
    /// Tallies a row that failed to import.
    /// </summary>
    /// <param name="reason">Why the row failed, such as <c>validation</c>.</param>
    public void AddError(string reason)
    {
        AddRow(ContentTransferDiagnostics.ResultError);
        Increment(_errors, reason);
    }

    /// <summary>
    /// Tallies a row whose import threw, and logs it for the first few rows of the run and then every hundredth. The
    /// exception's message can hold the row's values, so only its type is logged.
    /// </summary>
    /// <param name="rowIndex">The row of the file.</param>
    /// <param name="exception">The exception the row threw.</param>
    public void AddRowException(int rowIndex, Exception exception)
    {
        AddError(ContentTransferDiagnostics.ReasonHandlerException);
        _rowExceptions++;

        if ((_rowExceptions <= RowExceptionsLoggedInFull || _rowExceptions % RowExceptionLogInterval == 0)
            && _logger.IsEnabled(LogLevel.Warning))
        {
            ContentTransferLog.ImportRowFailed(_logger, rowIndex, _entryId, exception.GetType().FullName, _rowExceptions);
        }
    }

    /// <summary>
    /// Records the rows tallied since the progress was last saved, once the progress that includes them is saved.
    /// </summary>
    public void Commit()
    {
        foreach (var (result, count) in _rows)
        {
            ContentTransferDiagnostics.Add(ContentTransferDiagnostics.ImportRows, count, CrestAppsOrchardCoreTelemetry.ResultTag, result);
        }

        foreach (var (reason, count) in _skipped)
        {
            ContentTransferDiagnostics.Add(ContentTransferDiagnostics.ImportRowsSkipped, count, CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        }

        foreach (var (reason, count) in _errors)
        {
            ContentTransferDiagnostics.Add(ContentTransferDiagnostics.ImportRowErrors, count, CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
        }

        _rows.Clear();
        _skipped.Clear();
        _errors.Clear();
        PendingRows = 0;
    }

    /// <summary>
    /// Logs the progress of the run once per <see cref="ProgressLogInterval"/>.
    /// </summary>
    /// <param name="currentRow">The last row read.</param>
    /// <param name="totalRecords">The rows of the file.</param>
    public void LogProgress(int currentRow, int totalRecords)
    {
        var now = Stopwatch.GetTimestamp();

        if (Stopwatch.GetElapsedTime(_lastProgressLogTimestamp, now) < ProgressLogInterval
            || !_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        _lastProgressLogTimestamp = now;

        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp, now);
        var rowsPerSecond = elapsed > TimeSpan.Zero
            ? Math.Round((currentRow - _firstRowOfRun) / elapsed.TotalSeconds, 1)
            : 0;

        ContentTransferLog.ImportProgress(_logger, _entryId, currentRow, totalRecords, rowsPerSecond);
    }

    private static void Increment(Dictionary<string, long> counts, string key)
    {
        key ??= CrestAppsOrchardCoreTelemetry.Other;
        counts[key] = counts.TryGetValue(key, out var count)
            ? count + 1
            : 1;
    }
}
