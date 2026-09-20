using CrestApps.Core.ContactCenter;
using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Runs one pacing cycle for each enabled dialer profile so power and progressive campaigns dial automatically.
/// <para>
/// Each run is bounded by a wall-clock budget enforced both by a between-profile deadline check and a hard
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that cancels in-flight work, and that
/// budget is kept safely below the distributed-lock expiration so a slow run can never outlive its lock and let a
/// second node begin an overlapping pacing cycle. Profiles that do not fit in the budget are simply paced on the
/// following tick.
/// </para>
/// </summary>
public sealed class DialerPacingCycle : IDialerPacingCycle
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire, which
    /// guarantees the next scheduled tick cannot start an overlapping pacing cycle on another node.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IDialerProfileManager _dialerManager;
    private readonly IDialerService _dialerService;
    private readonly IQueueItemStore _queueItemStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="dialerManager">The dialer manager.</param>
    /// <param name="dialerService">The dialer service.</param>
    /// <param name="queueItemStore">The queue item store.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public DialerPacingCycle(
        IContactCenterFeatureWorkManager workManager,
        IDialerProfileManager dialerManager,
        IDialerService dialerService,
        IQueueItemStore queueItemStore,
        TimeProvider timeProvider,
        ILogger<DialerPacingCycle> logger)
    {
        _workManager = workManager;
        _dialerManager = dialerManager;
        _dialerService = dialerService;
        _queueItemStore = queueItemStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterCapabilities.DialerPaced);

        if (workLease is null)
        {
            return;
        }


        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(MaxRunDurationMilliseconds);
        var runToken = runCts.Token;

        var runDeadlineUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(MaxRunDurationMilliseconds);

        // Pacing is work-driven: a dialer profile is now reusable settings chosen when inventory is loaded, and
        // each loaded activity carries its profile on the queue item. So instead of iterating profiles, find the
        // campaign queues that actually have waiting outbound inventory and pace each one with the profile the
        // work was loaded under.
        IReadOnlyCollection<string> waitingQueueIds;

        try
        {
            waitingQueueIds = await _queueItemStore.GetWaitingQueueIdsAsync(runToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The dialer pacing run reached its {BudgetMilliseconds} ms time budget while listing queues; deferring to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }

            return;
        }

        var campaignQueueIds = waitingQueueIds
            .Where(ContactCenterConstants.IsCampaignQueue)
            .ToArray();

        foreach (var queueId in campaignQueueIds)
        {
            if (_timeProvider.GetUtcNow().UtcDateTime >= runDeadlineUtc)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The dialer pacing run reached its {BudgetMilliseconds} ms time budget; deferring the remaining queues to the next scheduled tick.",
                        MaxRunDurationMilliseconds);
                }

                break;
            }

            try
            {
                // Resolve the profile the queue's waiting inventory was loaded under from its head item. A campaign
                // is normally dialed by one profile; when several profiles share a campaign queue, the head item's
                // profile governs this cycle's pacing.
                var headItem = await _queueItemStore.FindNextWaitingAsync(queueId, runToken);

                if (headItem is null || string.IsNullOrEmpty(headItem.DialerProfileId))
                {
                    continue;
                }

                var profile = await _dialerManager.FindByIdAsync(headItem.DialerProfileId, runToken);

                if (profile is null)
                {
                    continue;
                }

                await _dialerService.RunCycleAsync(profile, queueId, runToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The dialer pacing run reached its {BudgetMilliseconds} ms time budget while pacing queue '{QueueId}'; deferring the remaining queues to the next scheduled tick.",
                        MaxRunDurationMilliseconds,
                        queueId);
                }

                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while pacing dialer queue '{QueueId}'.", queueId);
            }
        }
    }
}
