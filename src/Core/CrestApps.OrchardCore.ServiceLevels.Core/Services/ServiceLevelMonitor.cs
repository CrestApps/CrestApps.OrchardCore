using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

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

        var breached = await RaiseAsync(
            () => _store.ListNewlyBreachedAsync(now, BatchSize, cancellationToken),
            timer => timer.State == ServiceLevelTimerState.Running && timer.BreachedUtc is null && timer.DueUtc <= now,
            timer => timer.BreachedUtc = timer.DueUtc,
            (observer, timer) => observer.BreachedAsync(timer, cancellationToken),
            cancellationToken);

        var warned = await RaiseAsync(
            () => _store.ListNewlyWarnedAsync(now, BatchSize, cancellationToken),
            timer => timer.State == ServiceLevelTimerState.Running && timer.WarnedUtc is null && timer.BreachedUtc is null && timer.WarnAtUtc <= now && timer.DueUtc > now,
            timer => timer.WarnedUtc = now,
            (observer, timer) => observer.WarningAsync(timer, cancellationToken),
            cancellationToken);

        return breached + warned;
    }

    // Each due timer is marked in a transaction of its own, read right before the write, and only while it still needs
    // the mark: an owner's change (completing, pausing, retargeting) always wins, and owners never fail on the monitor.
    // Only a timer whose mark was saved is announced.
    private async Task<int> RaiseAsync(
        Func<Task<IReadOnlyList<ServiceLevelTimer>>> listDue,
        Func<ServiceLevelTimer, bool> stillDue,
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

            var ids = timers.Select(timer => timer.ItemId).ToList();

            // The list was read in this scope's transaction; ending it lets each mark take a transaction of its own.
            await _store.FlushAsync(cancellationToken);

            var marked = new List<ServiceLevelTimer>();

            foreach (var id in ids)
            {
                if (await _store.MarkIfStillDueAsync(id, stillDue, mark, cancellationToken) is { } saved)
                {
                    marked.Add(saved);
                }
                else
                {
                    _logger.LogDebug("A service level timer no longer needed its mark, or changed while it was being marked; it is read again on the next pass.");
                }
            }

            // The marks are saved; one observer that fails neither stops the others nor brings the mark back.
            foreach (var timer in marked)
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

            raised += marked.Count;

            if (timers.Count < BatchSize || marked.Count == 0)
            {
                break;
            }
        }

        return raised;
    }
}
