namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

/// <summary>
/// Tells the watchdog of an import run which row the run is working on and since when. The run writes it and the
/// watchdog reads it from another thread.
/// </summary>
internal sealed class ImportRunMonitor
{
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;

    private int _rowIndex;
    private long _rowStartedTimestamp;

    public ImportRunMonitor(TimeProvider timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Marks the start of the work on one row of the file.
    /// </summary>
    public void BeginRow(int rowIndex)
    {
        lock (_lock)
        {
            _rowIndex = rowIndex;
            _rowStartedTimestamp = _timeProvider.GetTimestamp();
        }
    }

    /// <summary>
    /// Marks the end of the work on the current row. Between rows the run waits on its own pacing, which is not a row
    /// that stopped responding.
    /// </summary>
    public void EndRow()
    {
        lock (_lock)
        {
            _rowIndex = 0;
        }
    }

    /// <summary>
    /// Gets the row the run is working on and how long it has been on it.
    /// </summary>
    /// <returns><see langword="false"/> when the run is between rows.</returns>
    public bool TryGetRowInFlight(out int rowIndex, out TimeSpan elapsed)
    {
        lock (_lock)
        {
            rowIndex = _rowIndex;
            elapsed = rowIndex > 0
                ? _timeProvider.GetElapsedTime(_rowStartedTimestamp)
                : TimeSpan.Zero;

            return rowIndex > 0;
        }
    }
}
