using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.BackgroundWork;

/// <summary>
/// Runs the batches of long-running background jobs one at a time across every tenant of the application, and
/// spreads them over time as set by <see cref="BackgroundWorkPacingOptions"/>.
/// </summary>
/// <remarks>
/// The gate is shared by the whole process, not owned by a tenant, because the tenants of one application
/// usually share one database. A batch holds the gate while it runs and through the pause that follows it, so
/// the bulk work of all tenants together uses about <see cref="BackgroundWorkPacingOptions.DatabaseShare"/> of
/// the database: one tenant deleting a list and ten tenants importing files load it the same, and each job takes
/// longer instead. Each application instance has its own gate, so a deployment scaled to several instances runs
/// one batch per instance.
/// </remarks>
public static class BackgroundWorkPacer
{
    /// <summary>
    /// The longest a batch waits for the gate before running without it.
    /// </summary>
    /// <remarks>
    /// The gate lives in memory, so a crash or restart can never leave it held, and it is released on every way a
    /// batch ends, including when its caller is cancelled while the batch itself keeps running. The one thing that
    /// could still hold it is a batch that hangs while its caller keeps waiting; giving up the wait after this long
    /// means such a batch slows the other jobs down but never stops them.
    /// </remarks>
    public static readonly TimeSpan GateWaitLimit = TimeSpan.FromMinutes(5);

    private static readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// The time a batch waited for the gate, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>:
    /// <c>acquired</c>, or <c>timeout</c> when it gave up and ran without the gate. It is the best measure of how
    /// busy the bulk work of all tenants keeps the database.
    /// </summary>
    public static Histogram<double> GateWaitDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        CrestAppsOrchardCoreTelemetry.PlatformMeter,
        "crestapps.background.pacer.gate_wait",
        "The time a background batch waited for the shared pacing gate.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of batches that gave up waiting for the gate after <see cref="GateWaitLimit"/> and ran without it.
    /// </summary>
    public static Counter<long> GateTimeouts { get; } = CrestAppsOrchardCoreTelemetry.PlatformMeter.CreateCounter<long>(
        "crestapps.background.pacer.gate_timeouts",
        "{batch}",
        "The number of background batches that gave up waiting for the shared pacing gate and ran without it.");

    /// <summary>
    /// The pause a batch earned and held the gate through, so bulk work keeps to its share of the database.
    /// </summary>
    public static Histogram<double> PauseDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        CrestAppsOrchardCoreTelemetry.PlatformMeter,
        "crestapps.background.pacer.pause",
        "The pause a background batch held the shared pacing gate through after it ran.");

    /// <summary>
    /// Waits for the gate, runs one batch, then keeps the gate through the pause that batch earned, so no other
    /// batch of any tenant starts before it is over.
    /// </summary>
    /// <typeparam name="TResult">The result of the batch.</typeparam>
    /// <param name="batch">The batch. It must not itself run a batch through this gate; if it does, the inner batch
    /// runs without the gate once <see cref="GateWaitLimit"/> has passed.</param>
    /// <param name="options">The pacing options of the tenant running the batch; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">Ends the wait for the gate, the batch and the pause when the host is stopping.</param>
    /// <returns>The result of the batch.</returns>
    public static Task<TResult> RunBatchAsync<TResult>(
        Func<CancellationToken, Task<TResult>> batch,
        BackgroundWorkPacingOptions options,
        CancellationToken cancellationToken = default)
        => RunBatchAsync(batch, options, GateWaitLimit, cancellationToken);

    /// <summary>
    /// Runs one batch through the gate, as <see cref="RunBatchAsync{TResult}(Func{CancellationToken, Task{TResult}}, BackgroundWorkPacingOptions, CancellationToken)"/>
    /// does, giving up the wait for the gate after <paramref name="gateWaitLimit"/> instead of <see cref="GateWaitLimit"/>.
    /// </summary>
    /// <typeparam name="TResult">The result of the batch.</typeparam>
    /// <param name="batch">The batch.</param>
    /// <param name="options">The pacing options of the tenant running the batch; <see langword="null"/> uses the defaults.</param>
    /// <param name="gateWaitLimit">The longest to wait for the gate before running the batch without it.</param>
    /// <param name="cancellationToken">Ends the wait for the gate, the batch and the pause when the host is stopping.</param>
    /// <returns>The result of the batch.</returns>
    public static async Task<TResult> RunBatchAsync<TResult>(
        Func<CancellationToken, Task<TResult>> batch,
        BackgroundWorkPacingOptions options,
        TimeSpan gateWaitLimit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        // A batch that cannot get the gate in time runs without it rather than waiting forever behind one that hung.
        var waitStarted = Stopwatch.GetTimestamp();
        var holdsGate = await _gate.WaitAsync(gateWaitLimit, cancellationToken);

        RecordGateWait(holdsGate, Stopwatch.GetElapsedTime(waitStarted));

        try
        {
            var started = TimeProvider.System.GetTimestamp();
            var batchTask = batch(cancellationToken);
            TResult result;

            try
            {
                // A batch that does not stop when it is cancelled used to keep the gate for as long as it hung, and
                // every other job of every tenant then waited out the full wait limit before each of its batches.
                // The caller stops waiting once it is cancelled, and the gate is released with it.
                result = await batchTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!batchTask.IsCompleted)
            {
                // Nobody awaits the batch any more; its failure, if it ends in one, is observed here.
                _ = batchTask.ContinueWith(
                    static task => _ = task.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                throw;
            }

            var pause = GetPause(TimeProvider.System.GetElapsedTime(started), options);

            if (pause > TimeSpan.Zero)
            {
                if (PauseDuration.Enabled)
                {
                    PauseDuration.Record(pause.TotalSeconds);
                }

                await Task.Delay(pause, cancellationToken);
            }

            return result;
        }
        finally
        {
            // Released however the batch ended: completed, threw, or was cancelled during the batch or the pause.
            if (holdsGate)
            {
                _gate.Release();
            }
        }
    }

    private static void RecordGateWait(bool holdsGate, TimeSpan waited)
    {
        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, holdsGate ? "acquired" : CrestAppsOrchardCoreTelemetry.OutcomeTimeout },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        if (GateWaitDuration.Enabled)
        {
            GateWaitDuration.Record(waited.TotalSeconds, tags);
        }

        if (!holdsGate && GateTimeouts.Enabled)
        {
            GateTimeouts.Add(1, tags);
        }
    }

    /// <summary>
    /// Gets how long to wait after a batch that took <paramref name="batchDuration"/>, so that bulk work runs for
    /// about <see cref="BackgroundWorkPacingOptions.DatabaseShare"/> of the time.
    /// </summary>
    /// <param name="batchDuration">How long the batch just finished took.</param>
    /// <param name="options">The pacing options; <see langword="null"/> uses the defaults.</param>
    /// <returns>The pause, never longer than <see cref="BackgroundWorkPacingOptions.MaxPause"/>.</returns>
    public static TimeSpan GetPause(TimeSpan batchDuration, BackgroundWorkPacingOptions options)
    {
        options ??= new BackgroundWorkPacingOptions();

        var share = Math.Clamp(options.DatabaseShare, BackgroundWorkPacingOptions.MinimumDatabaseShare, 1d);

        if (share >= 1d || batchDuration <= TimeSpan.Zero || options.MaxPause <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var pause = batchDuration * ((1d - share) / share);

        return pause > options.MaxPause ? options.MaxPause : pause;
    }
}
