namespace CrestApps.Core.Hosting;

/// <summary>
/// Collects work that must not run until the current unit of work has been committed, so a failed
/// commit cannot leave the outside world believing something happened that did not.
/// </summary>
/// <remarks>
/// The queue is scoped. Whoever owns the commit drains it once the commit succeeds, and discards it
/// when the commit fails.
/// </remarks>
public interface IAfterCommitTaskQueue
{
    /// <summary>
    /// Gets whether any work is waiting.
    /// </summary>
    bool HasPendingWork { get; }

    /// <summary>
    /// Adds work to run after the commit succeeds.
    /// </summary>
    /// <param name="operation">The work to run.</param>
    void Enqueue(Func<IServiceProvider, Task> operation);

    /// <summary>
    /// Runs and clears every queued operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider handed to each operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DrainAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}
