using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Re-arms the in-process routing deadlines of the work that was open when the tenant started: every offer still
/// ringing, every waiting caller's next overflow hop, maximum wait or held-call ring window, and each queue's next
/// treatment step. Shortly afterwards it makes one reservation-and-assignment pass, so callers who waited through the
/// restart are offered to the agents who are free.
/// </summary>
/// <remarks>
/// The deadlines are held in process, so a restart loses them. Until the sweeps next ran, an offer that was already
/// ringing kept ringing past its window — the ring-timeout sweep runs once a minute — and a caller past their maximum
/// wait kept waiting. The work is deferred until activation has finished: run during activation, the scope it needs
/// waits on the very tenant that is still starting, and the tenant never finishes starting.
/// <para>
/// Re-arming restores the deadlines but offers nobody anything. An agent whose soft phone reconnects asks for the
/// waiting voice work itself, but every other waiting item, and any caller whose agent's reconnect request failed,
/// sat until the assignment sweep's next tick, up to a minute after the tenant came back.
/// </para>
/// </remarks>
internal sealed class ContactCenterDeadlineRearm : ModularTenantEvents
{
    private const int PageSize = 200;

    /// <summary>
    /// The key the post-restart assignment pass is held under in the deadline scheduler.
    /// </summary>
    internal const string StartupAssignmentKey = "startup-assignment";

    /// <summary>
    /// How long after the tenant starts the assignment pass runs.
    /// </summary>
    /// <remarks>
    /// An agent's last heartbeat from before the restart keeps them counted as live for up to ninety seconds, so a
    /// pass run at once would ring agents whose soft phones have not reconnected yet. Soft phones retry at once and
    /// then after two and five seconds, so ten seconds lets them back first and still runs well ahead of the sweep.
    /// </remarks>
    internal static readonly TimeSpan StartupAssignmentDelay = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger;

    public ContactCenterDeadlineRearm(ILogger<ContactCenterDeadlineRearm> logger)
    {
        _logger = logger;
    }

    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            await RearmAsync(scope.ServiceProvider, _logger, CancellationToken.None);

            ScheduleStartupAssignment(scope.ServiceProvider);
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Schedules the one assignment pass that follows a restart.
    /// </summary>
    internal static void ScheduleStartupAssignment(IServiceProvider services)
    {
        var dueUtc = services.GetRequiredService<IClock>().UtcNow + StartupAssignmentDelay;

        services.GetRequiredService<IContactCenterDeadlineScheduler>().Schedule(StartupAssignmentKey, dueUtc, RunStartupAssignmentAsync);
    }

    /// <summary>
    /// Makes the post-restart assignment pass, unless the sweep is already making one.
    /// </summary>
    internal static async Task<DateTime?> RunStartupAssignmentAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // The sweep's own lock, so the pass never runs alongside a sweep doing the same work on any node.
        var settings = new ReservationExpiryBackgroundTask().GetDefaultSettings();
        (var locker, var locked) = await services.GetRequiredService<IDistributedLock>().TryAcquireBackgroundTaskLockAsync(settings);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        await ReservationExpiryBackgroundTask.RunAsync(services, cancellationToken);

        return null;
    }

    /// <summary>
    /// Arms the deadline of every offer and waiting caller the durable state holds open.
    /// </summary>
    internal static async Task RearmAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        // Each half on its own: an offer that cannot be read is no reason to leave the callers without their deadlines.
        try
        {
            await RearmOffersAsync(services, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not re-arm the deadlines of ringing offers; the ring-timeout sweep will expire them.");
        }

        try
        {
            await RearmWaitingCallersAsync(services, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not re-arm the deadlines of waiting callers; the queue-treatment sweep will enforce them.");
        }
    }

    private static async Task RearmOffersAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var reservationManager = services.GetRequiredService<IActivityReservationManager>();
        var scheduler = services.GetRequiredService<IContactCenterDeadlineScheduler>();

        // Every pending offer due within the scheduler's lead time, the ones already past due included: those expire
        // at once. Anything further out is left to the sweep, as the scheduler would leave it.
        var horizonUtc = services.GetRequiredService<IClock>().UtcNow + ContactCenterDeadlineScheduler.MaximumLeadTime;
        DateTime? afterExpiresUtc = null;
        long afterDocumentId = 0;

        while (true)
        {
            var page = await reservationManager.GetExpiredAsync(horizonUtc, afterExpiresUtc, afterDocumentId, PageSize, cancellationToken);

            foreach (var reservation in page.Reservations)
            {
                if (reservation.Status == ReservationStatus.Pending)
                {
                    scheduler.Schedule(
                        OfferDeadlineEventHandler.GetDeadlineKey(reservation.ItemId),
                        reservation.ExpiresUtc,
                        OfferDeadlineEventHandler.CreateExpiry(reservation.ItemId));
                }
            }

            if (!page.HasMore)
            {
                return;
            }

            afterExpiresUtc = page.NextAfterExpiresUtc;
            afterDocumentId = page.NextAfterDocumentId;
        }
    }

    private static async Task RearmWaitingCallersAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var queueItemStore = services.GetRequiredService<IQueueItemStore>();
        var queueItemManager = services.GetRequiredService<IQueueItemManager>();
        var waitEnforcer = services.GetRequiredService<IQueueWaitDeadlineEnforcer>();
        var treatmentEnforcer = services.GetRequiredService<IQueueTreatmentDeadlineEnforcer>();

        foreach (var queueId in await queueItemStore.GetWaitingQueueIdsAsync(cancellationToken))
        {
            foreach (var item in await queueItemManager.GetWaitingAsync(queueId, cancellationToken))
            {
                await waitEnforcer.ArmAsync(item.ItemId, cancellationToken);
            }

            await treatmentEnforcer.ArmAsync(queueId, cancellationToken);
        }
    }
}
