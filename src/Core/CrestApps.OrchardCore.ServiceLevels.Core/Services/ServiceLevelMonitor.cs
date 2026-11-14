using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Finds the running timers that reached their warning point or passed their target, marks them, and tells the
/// observers. It reads only the timers that are due, a batch at a time, and saves a batch before it tells anyone, so
/// a timer its owner changed in the same moment is left for the next pass instead of being written over.
/// </summary>
public sealed class ServiceLevelMonitor
{
    private const int BatchSize = 100;
    private const int MaxBatches = 20;

    private readonly IServiceLevelTimerStore _store;
    private readonly IEnumerable<IServiceLevelObserver> _observers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelMonitor"/> class.
    /// </summary>
    /// <param name="store">The timer store.</param>
    /// <param name="observers">The timer observers.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public ServiceLevelMonitor(
        IServiceLevelTimerStore store,
        IEnumerable<IServiceLevelObserver> observers,
        IClock clock,
        ILogger<ServiceLevelMonitor> logger)
    {
        _store = store;
        _observers = observers;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Raises the breaches and the warnings that are due.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many breaches and warnings were raised.</returns>
    public async Task<int> CheckAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var (breached, interrupted) = await RaiseAsync(
            () => _store.ListNewlyBreachedAsync(now, BatchSize, cancellationToken),
            timer => timer.BreachedUtc = timer.DueUtc,
            (observer, timer) => observer.BreachedAsync(timer, cancellationToken),
            cancellationToken);

        if (interrupted)
        {
            return breached;
        }

        var (warned, _) = await RaiseAsync(
            () => _store.ListNewlyWarnedAsync(now, BatchSize, cancellationToken),
            timer => timer.WarnedUtc = now,
            (observer, timer) => observer.WarningAsync(timer, cancellationToken),
            cancellationToken);

        return breached + warned;
    }

    private async Task<(int Raised, bool Interrupted)> RaiseAsync(
        Func<Task<IReadOnlyList<ServiceLevelTimer>>> listDue,
        Action<ServiceLevelTimer> mark,
        Func<IServiceLevelObserver, ServiceLevelTimer, Task> notify,
        CancellationToken cancellationToken)
    {
        var raised = 0;

        for (var batch = 0; batch < MaxBatches && !cancellationToken.IsCancellationRequested; batch++)
        {
            var timers = await listDue();

            if (timers.Count == 0)
            {
                break;
            }

            foreach (var timer in timers)
            {
                mark(timer);
                await _store.UpdateIfUnchangedAsync(timer, cancellationToken);
            }

            try
            {
                await _store.FlushAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                // An owner changed one of these timers while they were being marked. Nothing was saved and nobody was
                // told; the next pass reads them again.
                _logger.LogDebug("A service level timer changed while the monitor was marking a batch; the batch is retried on the next pass.");

                return (raised, true);
            }

            // The marks are saved; one observer that fails neither stops the others nor brings the mark back.
            foreach (var timer in timers)
            {
                foreach (var observer in _observers)
                {
                    try
                    {
                        await notify(observer, timer);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        _logger.LogError(exception, "The service level observer {Observer} failed on the timer {TimerId}.", observer.GetType().Name, timer.ItemId);
                    }
                }
            }

            raised += timers.Count;

            if (timers.Count < BatchSize)
            {
                break;
            }
        }

        return (raised, false);
    }
}
