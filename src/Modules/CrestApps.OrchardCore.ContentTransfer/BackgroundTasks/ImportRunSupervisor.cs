using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

/// <summary>
/// How long an import run may take before its watchdog gives up on it.
/// </summary>
/// <param name="RunTimeout">The longest one run may take. It stays below the expiration of the import lock, so another
/// instance never starts on the file while this run is still at work.</param>
/// <param name="RowTimeout">The longest one row of the file may take before the run is considered stuck.</param>
/// <param name="SlowRowThreshold">How long a row may take before a warning names it.</param>
/// <param name="CheckInterval">How often the watchdog looks at the run.</param>
internal sealed record ImportRunLimits(
    TimeSpan RunTimeout,
    TimeSpan RowTimeout,
    TimeSpan SlowRowThreshold,
    TimeSpan CheckInterval);

/// <summary>
/// The outcome of a supervised import run.
/// </summary>
/// <param name="Abandoned"><see langword="true"/> when the watchdog gave up on the run, or the host is stopping.</param>
/// <param name="Result">What the run returned; the default when it was abandoned.</param>
/// <param name="StalledRowIndex">The row the run was stuck on when it was abandoned; 0 when it was between rows.</param>
internal readonly record struct SupervisedImportRun<TResult>(bool Abandoned, TResult Result, int StalledRowIndex);

/// <summary>
/// Runs an import under a watchdog, so a row that never finishes cannot keep the import lock forever.
/// </summary>
/// <remarks>
/// A run used to be awaited for as long as it took. A row that never finished (a call that never returns, for
/// example) kept the run waiting, and the run kept the import lock: with the in-memory lock, which ignores its
/// expiration, that lasted until the application restarted. Every Resume and every scheduled run found the lock taken
/// and returned without a word, and the import sat at the same row with no error. The watchdog stops waiting for a run
/// that is stuck on one row or that outlives its time, cancels it so it can no longer save progress, and returns, so
/// the caller releases the lock and the next run continues from the last saved row.
/// </remarks>
internal static class ImportRunSupervisor
{
    public static async Task<SupervisedImportRun<TResult>> RunAsync<TResult>(
        Func<ImportRunMonitor, CancellationToken, Task<TResult>> run,
        ImportRunLimits limits,
        ILogger logger,
        string entryId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(logger);

        var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var monitor = new ImportRunMonitor();
        var started = Stopwatch.GetTimestamp();

        // Started on the thread pool, so a row stuck in synchronous work cannot hold the watchdog either.
        var work = Task.Run(() => run(monitor, runCts.Token), CancellationToken.None);
        var warnedRowIndex = 0;

        while (true)
        {
            await Task.WhenAny(work, Task.Delay(limits.CheckInterval, cancellationToken));

            if (work.IsCompleted)
            {
                runCts.Dispose();

                return new SupervisedImportRun<TResult>(false, await work, 0);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // The host is stopping and the run did not stop with it. It resumes from its saved row next time.
                return Abandon(work, runCts, logger, entryId, started, stalledRowIndex: 0);
            }

            var hasRowInFlight = monitor.TryGetRowInFlight(out var rowIndex, out var rowElapsed);

            if (hasRowInFlight && rowElapsed >= limits.RowTimeout)
            {
                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(
                        "The import of entry '{EntryId}' stopped responding on row {RowIndex}, which has been running for {RowElapsed}. The run was abandoned and its lock released; the next run resumes from the last saved row.",
                        entryId,
                        rowIndex,
                        rowElapsed);
                }

                return Abandon(work, runCts, logger, entryId, started, rowIndex);
            }

            var runElapsed = Stopwatch.GetElapsedTime(started);

            if (runElapsed >= limits.RunTimeout)
            {
                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(
                        "The import of entry '{EntryId}' has run for {RunElapsed}, past its limit of {RunTimeout}, and was abandoned so its lock does not expire while it still runs. It was on row {RowIndex}. The next run resumes from the last saved row.",
                        entryId,
                        runElapsed,
                        limits.RunTimeout,
                        hasRowInFlight ? rowIndex : null);
                }

                return Abandon(work, runCts, logger, entryId, started, hasRowInFlight ? rowIndex : 0);
            }

            if (hasRowInFlight && rowElapsed >= limits.SlowRowThreshold && rowIndex != warnedRowIndex)
            {
                warnedRowIndex = rowIndex;

                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        "The import of entry '{EntryId}' has been on row {RowIndex} for {RowElapsed}. The run is abandoned if the row is still running after {RowTimeout}.",
                        entryId,
                        rowIndex,
                        rowElapsed,
                        limits.RowTimeout);
                }
            }
        }
    }

    private static SupervisedImportRun<TResult> Abandon<TResult>(
        Task<TResult> work,
        CancellationTokenSource runCts,
        ILogger logger,
        string entryId,
        long started,
        int stalledRowIndex)
    {
        // Cancelled before anything else: the run checks the token before it saves a batch, so if it ever wakes up it
        // cannot record progress over the run that replaces it.
        runCts.Cancel();

        // The source is disposed only once the run has ended, since the run still holds its token.
        _ = work.ContinueWith(
            completed =>
            {
                runCts.Dispose();

                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        completed.Exception?.GetBaseException(),
                        "The abandoned run of the import of entry '{EntryId}' ended after {Elapsed}, with status {Status}.",
                        entryId,
                        Stopwatch.GetElapsedTime(started),
                        completed.Status);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return new SupervisedImportRun<TResult>(true, default, stalledRowIndex);
    }
}
