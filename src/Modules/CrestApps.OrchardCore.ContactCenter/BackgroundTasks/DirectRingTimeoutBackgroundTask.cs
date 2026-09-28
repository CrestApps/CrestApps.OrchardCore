using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// The durable backstop for agent ring windows: once a minute it expires every reservation past its deadline and
/// times out every held direct-to-agent call whose ring window has elapsed.
/// </summary>
/// <remarks>
/// Ring windows are enforced to the second by the in-process deadlines <see cref="IContactCenterDeadlineScheduler"/>
/// holds for every offer and every held call; this sweep catches only what those missed — a restart, an offer made
/// on another node, a deadline beyond the scheduler's lead time. It used to tick internally for most of a minute to
/// give sub-minute precision, but Orchard runs a tenant's background tasks one after another, so holding the loop
/// that long delayed every other task behind it, the reservation expiry sweep included: a thirty-second offer rang
/// for seventy-three. It now makes one pass and returns the loop. Every sweep is bounded and idempotent: expiry takes
/// a per-reservation lock and skips anything another path is already transitioning.
/// </remarks>
[BackgroundTask(
    Title = "Contact Center Ring Timeout Enforcement",
    Schedule = "* * * * *",
    Description = "Expires unanswered agent offers and held direct calls that their in-process deadline missed.",
    LockTimeout = 1_000,
    LockExpiration = 60_000)]
public sealed class DirectRingTimeoutBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        var scopeExecutor = serviceProvider.GetRequiredService<IContactCenterScopeExecutor>();
        var logger = serviceProvider.GetRequiredService<ILogger<DirectRingTimeoutBackgroundTask>>();

        // The direct-to-agent hold timeout is only registered when the Voice feature is enabled; without it there
        // is still value in expiring due reservations, so the sweep runs either way.
        var hasDirectHoldTimeout = serviceProvider.GetService<IDirectHoldTimeoutService>() is not null;

        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Queues);

        if (workLease is null)
        {
            return;
        }

        try
        {
            // Each pass on its own committed scope, so the provider commands a pass schedules (the actual "send to
            // voicemail") dispatch after that pass commits rather than after the whole run.
            await scopeExecutor.ExecuteAsync<IActivityReservationService>(
                reservationService => reservationService.ExpireDueAsync(cancellationToken));

            if (hasDirectHoldTimeout)
            {
                await scopeExecutor.ExecuteAsync<IDirectHoldTimeoutService>(
                    directHoldTimeoutService => directHoldTimeoutService.ProcessDueAsync(cancellationToken));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while enforcing the direct-to-agent ring timeout.");
        }
    }
}
