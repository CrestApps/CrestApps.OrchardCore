using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Bounds how many report runs a tenant runs at once, to <see cref="ReportQueryLimits.MaxConcurrentRuns"/>: the others
/// wait for one to end. A run that reads a view runs the view within its own run, so it doesn't wait for itself.
/// </summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The semaphore is only awaited, never asked for its wait handle, so disposing it would free nothing; the gate lives as long as the tenant.")]
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
        var tags = ReportTelemetry.Tags(context);

        cancellationToken.ThrowIfCancellationRequested();

        // A run that gets the gate at once doesn't wait, and isn't counted among the runs that wait.
        if (!semaphore.Wait(0, CancellationToken.None))
        {
            var started = Stopwatch.GetTimestamp();

            ReportTelemetry.GateWaiting.Add(1, tags);

            try
            {
                await semaphore.WaitAsync(cancellationToken);
            }
            finally
            {
                ReportTelemetry.GateWaiting.Add(-1, tags);
                ReportTelemetry.GateWait.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
            }
        }
        else
        {
            ReportTelemetry.GateWait.Record(0, tags);
        }

        properties[RunKey] = true;
        ReportTelemetry.GateActive.Add(1, tags);

        return new Lease(semaphore, properties, tags);
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
        public static readonly Lease None = new(null, null, default);

        private readonly SemaphoreSlim _semaphore;
        private readonly IDictionary<string, object> _properties;
        private readonly TagList _tags;

        public Lease(
            SemaphoreSlim semaphore,
            IDictionary<string, object> properties,
            TagList tags)
        {
            _semaphore = semaphore;
            _properties = properties;
            _tags = tags;
        }

        public ValueTask DisposeAsync()
        {
            if (_semaphore is not null)
            {
                _properties.Remove(RunKey);
                _semaphore.Release();
                ReportTelemetry.GateActive.Add(-1, _tags);
            }

            return ValueTask.CompletedTask;
        }
    }
}
