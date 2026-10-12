using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Settings;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Checkout.Tasks;

/// <summary>
/// Periodically reconciles checkout sessions that still have non-terminal payment attempts against the
/// payment providers' authoritative APIs. This is the crash-recovery safety net: if a node crashes, a
/// distributed cache entry is evicted, a customer abandons a redirect, or a webhook is lost, the durable
/// attempt is still swept up here and settled or failed based on what really happened at the gateway, so
/// a real charge is never left unrecorded.
/// </summary>
[BackgroundTask(
    Title = "Checkout Payment Reconciliation",
    Schedule = "*/5 * * * *",
    Description = "Reconciles pending checkout payment attempts against their providers.",
    LockTimeout = 3_000,
    LockExpiration = 60_000)]
public sealed class CheckoutReconciliationBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _minimumAge = TimeSpan.FromMinutes(2);

    private const int MaxSessionsPerRun = 100;

    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutReconciliationBackgroundTask"/> class.
    /// </summary>
    /// <param name="clock">The clock used to calculate the cutoff time for pending attempts.</param>
    public CheckoutReconciliationBackgroundTask(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Reconciles checkout sessions with pending payment attempts that are old enough to be swept.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve scoped checkout services.</param>
    /// <param name="cancellationToken">The token used to stop the reconciliation sweep.</param>
    /// <returns>A task that represents the asynchronous background operation.</returns>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var attemptStore = serviceProvider.GetRequiredService<IPaymentAttemptStore>();
        var engine = serviceProvider.GetRequiredService<ICheckoutEngine>();
        var logger = serviceProvider.GetRequiredService<ILogger<CheckoutReconciliationBackgroundTask>>();

        var sessionStore = serviceProvider.GetRequiredService<ICheckoutSessionStore>();
        var siteService = serviceProvider.GetRequiredService<ISiteService>();

        var olderThanUtc = _clock.UtcNow - _minimumAge;
        var pending = await attemptStore.GetPendingAsync(olderThanUtc, cancellationToken);

        // A checkout whose payment was confirmed but whose fulfillment failed has no pending attempt to be
        // found by, so it is found by its own status instead. Without this, a customer who paid moments
        // before a database hiccup would have a confirmed charge and nothing delivered, forever.
        var stuck = await sessionStore.GetStaleAsync(CheckoutSessionStatus.PaymentPending, olderThanUtc, MaxSessionsPerRun, cancellationToken);

        var sessionIds = pending
            .Select(attempt => attempt.SessionId)
            .Concat(stuck.Select(session => session.SessionId))
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal);

        foreach (var sessionId in sessionIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                // Completing rather than merely reconciling is what makes this a recovery path instead of a
                // bookkeeping job. A customer who paid and then closed the browser has a settled charge and an
                // unfulfilled purchase; verifying the attempt without running completion would leave it that
                // way forever. The engine is idempotent, so a session the customer finished in the meantime is
                // reported as already completed and nothing runs twice.
                var result = await engine.TryCompleteAsync(sessionId, cancellationToken);

                if (result.Status == CheckoutCompletionStatus.Completed && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Checkout session '{SessionId}' was completed by the reconciliation sweep after the customer left the payment flow.", sessionId);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to reconcile checkout session '{SessionId}' during the background sweep.", sessionId);
            }
        }

        await ExpireAbandonedAsync(sessionStore, siteService, engine, logger, cancellationToken);
    }

    // A checkout nobody finished still holds whatever the provider set aside for it: an authorization, a
    // pending intent, a subscription waiting on confirmation. Releasing those after the configured lifetime
    // is what keeps the provider's side from filling with the site's forgotten checkouts. The engine refuses
    // to expire anything that actually collected money, so this can never close a paid purchase.
    private static async Task ExpireAbandonedAsync(
        ICheckoutSessionStore sessionStore,
        ISiteService siteService,
        ICheckoutEngine engine,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var settings = await siteService.GetSettingsAsync<CheckoutSettings>();
        var lifetime = TimeSpan.FromHours(Math.Max(1, settings.SessionLifetimeHours));
        var before = DateTime.UtcNow - lifetime;

        foreach (var status in new[] { CheckoutSessionStatus.Pending, CheckoutSessionStatus.AwaitingProvider })
        {
            var abandoned = await sessionStore.GetStaleAsync(status, before, MaxSessionsPerRun, cancellationToken);

            foreach (var session in abandoned)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await engine.ExpireAsync(session.SessionId, cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to expire abandoned checkout session '{SessionId}'.", session.SessionId);
                }
            }
        }
    }
}
