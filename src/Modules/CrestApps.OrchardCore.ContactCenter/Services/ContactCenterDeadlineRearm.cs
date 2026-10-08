using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Re-arms the in-process routing deadlines of the work that was open when the tenant started: every offer still
/// ringing, every waiting caller's next overflow hop, maximum wait or held-call ring window, and each queue's next
/// treatment step.
/// </summary>
/// <remarks>
/// The deadlines are held in process, so a restart loses them. Until the sweeps next ran, an offer that was already
/// ringing kept ringing past its window — the ring-timeout sweep runs once a minute — and a caller past their maximum
/// wait kept waiting. The work is deferred until activation has finished: run during activation, the scope it needs
/// waits on the very tenant that is still starting, and the tenant never finishes starting.
/// </remarks>
internal sealed class ContactCenterDeadlineRearm : ModularTenantEvents
{
    private const int PageSize = 200;

    private readonly ILogger _logger;

    public ContactCenterDeadlineRearm(ILogger<ContactCenterDeadlineRearm> logger)
    {
        _logger = logger;
    }

    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(scope => RearmAsync(scope.ServiceProvider, _logger, CancellationToken.None));

        return Task.CompletedTask;
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
