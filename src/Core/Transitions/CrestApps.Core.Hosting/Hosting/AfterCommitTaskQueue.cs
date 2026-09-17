namespace CrestApps.Core.Hosting;

/// <summary>
/// The default <see cref="IAfterCommitTaskQueue"/>: an ordered, scoped list of operations drained by
/// whoever owns the commit.
/// </summary>
public sealed class AfterCommitTaskQueue : IAfterCommitTaskQueue
{
    private readonly List<Func<IServiceProvider, Task>> _operations = [];

    /// <inheritdoc/>
    public bool HasPendingWork => _operations.Count > 0;

    /// <inheritdoc/>
    public void Enqueue(Func<IServiceProvider, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        _operations.Add(operation);
    }

    /// <inheritdoc/>
    public async Task DrainAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // An operation may enqueue more work, so drain until the list settles rather than iterating
        // a snapshot and dropping whatever the last pass added.
        while (_operations.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pending = _operations.ToArray();
            _operations.Clear();

            foreach (var operation in pending)
            {
                await operation(serviceProvider);
            }
        }
    }
}
