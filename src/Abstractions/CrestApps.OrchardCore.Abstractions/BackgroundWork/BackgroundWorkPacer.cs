namespace CrestApps.OrchardCore.BackgroundWork;

/// <summary>
/// Spreads the batches of a long-running background job over time, as set by <see cref="BackgroundWorkPacingOptions"/>.
/// </summary>
public static class BackgroundWorkPacer
{
    /// <summary>
    /// Gets how long to wait after a batch that took <paramref name="batchDuration"/>, so that the job works for
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

    /// <summary>
    /// Waits after a batch that took <paramref name="batchDuration"/>, as <see cref="GetPause"/> decides.
    /// </summary>
    /// <param name="batchDuration">How long the batch just finished took.</param>
    /// <param name="options">The pacing options; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">Ends the wait early when the host is stopping.</param>
    /// <returns>A task that completes when the pause is over.</returns>
    public static Task PauseAfterBatchAsync(
        TimeSpan batchDuration,
        BackgroundWorkPacingOptions options,
        CancellationToken cancellationToken = default)
    {
        var pause = GetPause(batchDuration, options);

        return pause > TimeSpan.Zero
            ? Task.Delay(pause, cancellationToken)
            : Task.CompletedTask;
    }
}
