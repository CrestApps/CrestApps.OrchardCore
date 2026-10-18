namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Bounds how many report runs a tenant runs at once, to <see cref="ReportQueryLimits.MaxConcurrentRuns"/>: the others
/// wait for one to end. A run that reads a view runs the view within its own run, so it doesn't wait for itself.
/// </summary>
public sealed class ReportQueryGate
{
    // Marks the data source context of a run, which the views it reads share.
    private const string RunKey = "CrestApps.Reports.Run";

    private readonly object _lock = new();
    private SemaphoreSlim _semaphore;
    private int _size;

    /// <summary>
    /// Waits for a run to start, unless the context is already in one.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Ends the run when disposed.</returns>
    public async Task<IAsyncDisposable> EnterAsync(ReportQueryExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var properties = context.DataSourceContext?.Properties;

        if (properties is null || properties.ContainsKey(RunKey))
        {
            return Lease.None;
        }

        var semaphore = GetSemaphore(context.Limits.MaxConcurrentRuns);

        await semaphore.WaitAsync(cancellationToken);

        properties[RunKey] = true;

        return new Lease(semaphore, properties);
    }

    private SemaphoreSlim GetSemaphore(int size)
    {
        size = Math.Max(1, size);

        lock (_lock)
        {
            // The limit is read from the settings of the tenant, which only change when the tenant restarts.
            if (_semaphore is null || _size != size)
            {
                _semaphore = new SemaphoreSlim(size, size);
                _size = size;
            }

            return _semaphore;
        }
    }

    private sealed class Lease : IAsyncDisposable
    {
        public static readonly Lease None = new(null, null);

        private readonly SemaphoreSlim _semaphore;
        private readonly IDictionary<string, object> _properties;

        public Lease(SemaphoreSlim semaphore, IDictionary<string, object> properties)
        {
            _semaphore = semaphore;
            _properties = properties;
        }

        public ValueTask DisposeAsync()
        {
            if (_semaphore is not null)
            {
                _properties.Remove(RunKey);
                _semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
