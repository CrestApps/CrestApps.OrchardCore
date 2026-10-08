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
    /// batch ends. The one thing that could still hold it is a batch that hangs instead of failing; giving up the
    /// wait after this long means such a batch slows the other jobs down but never stops them.
    /// </remarks>
    public static readonly TimeSpan GateWaitLimit = TimeSpan.FromMinutes(5);

    private static readonly SemaphoreSlim _gate = new(1, 1);

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
        var holdsGate = await _gate.WaitAsync(gateWaitLimit, cancellationToken);

        try
        {
            var started = TimeProvider.System.GetTimestamp();
            var result = await batch(cancellationToken);
            var pause = GetPause(TimeProvider.System.GetElapsedTime(started), options);

            if (pause > TimeSpan.Zero)
            {
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
