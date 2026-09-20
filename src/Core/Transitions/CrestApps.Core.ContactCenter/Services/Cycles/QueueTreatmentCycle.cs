using CrestApps.Core.ContactCenter;
using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Plays what waiting callers are due to hear, and hands on the callers whose overflow tier has come due.
/// </summary>
public sealed class QueueTreatmentCycle : IQueueTreatmentCycle
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Twice the one-minute schedule, so the lock is not
    /// released while a run is still sweeping.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The wall-clock budget for one run. Kept below the lock expiration so a run always finishes before its lock
    /// can expire.
    /// </summary>
    private static readonly TimeSpan _runBudget = TimeSpan.FromSeconds(50);

    /// <summary>
    /// How long to wait between sweeps. This is the precision the acceptance criterion asks for: a caller
    /// overflows within ten seconds of their threshold rather than up to a minute after it.
    /// </summary>
    private static readonly TimeSpan _sweepInterval = TimeSpan.FromSeconds(10);

    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly ILogger _logger;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueTreatmentCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="scopeExecutor">The scope executor. Each sweep runs and commits on its own scope.</param>
    public QueueTreatmentCycle(
        IContactCenterFeatureWorkManager workManager,
        ILogger<QueueTreatmentCycle> logger,
        IContactCenterScopeExecutor scopeExecutor)
    {
        _workManager = workManager;
        _logger = logger;
        _scopeExecutor = scopeExecutor;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterCapabilities.Queues);

        if (workLease is null)
        {
            return;
        }


        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(_runBudget);
        var runToken = runCts.Token;

        try
        {
            while (!runToken.IsCancellationRequested)
            {
                await _scopeExecutor.ExecuteAsync(scoped => SweepAsync(scoped, _logger, runToken));

                await Task.Delay(_sweepInterval, runToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The run budget expired or the tenant is shutting down. Both end the run normally: the next
            // scheduled tick picks the sweep back up.
        }
    }

    /// <summary>
    /// Sweeps every queue once.
    /// </summary>
    private static async Task SweepAsync(IServiceProvider serviceProvider, ILogger logger, CancellationToken runToken)
    {
        // Resolved from the sweep's own scope rather than the cycle's, because each sweep commits and
        // the services it drives are scoped to that unit of work.
        var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
        var treatmentService = serviceProvider.GetRequiredService<IQueueTreatmentService>();
        var queueService = serviceProvider.GetRequiredService<IActivityQueueService>();
        var limitService = serviceProvider.GetRequiredService<IQueueLimitService>();

        var queues = await queueManager.GetAllAsync(runToken);

        foreach (var queue in queues)
        {
            if (runToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await treatmentService.RunDueAsync(queue, runToken);
                await queueService.OverflowDueAsync(queue, runToken);

                // After the overflow tiers, so a caller whose hop and maximum wait fall due together is
                // handed on rather than sent to voicemail.
                await limitService.EnforceMaxWaitAsync(queue, runToken);
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One misconfigured queue must not stop the sweep for every other queue on the tenant.
                logger.LogError(ex, "A queue-treatment pass failed for one queue; the remaining queues are still swept.");
            }
        }
    }
}
