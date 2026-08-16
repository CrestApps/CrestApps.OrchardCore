using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

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
        var sessionStore = serviceProvider.GetRequiredService<ICheckoutSessionStore>();
        var reconciliationService = serviceProvider.GetRequiredService<ICheckoutReconciliationService>();
        var logger = serviceProvider.GetRequiredService<ILogger<CheckoutReconciliationBackgroundTask>>();

        var olderThanUtc = _clock.UtcNow - _minimumAge;
        var pending = await attemptStore.GetPendingAsync(olderThanUtc, cancellationToken);

        var sessionIds = pending
            .Select(attempt => attempt.SessionId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal);

        foreach (var sessionId in sessionIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var session = await sessionStore.GetAsync(sessionId, cancellationToken);

            if (session == null)
            {
                continue;
            }

            try
            {
                // The reconciliation service derives the obligations from the durable attempts on the
                // session, so an empty expected set is sufficient here.
                await reconciliationService.ReconcileAsync(session, [], cancellationToken);
                await sessionStore.SaveAsync(session, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to reconcile checkout session '{SessionId}' during the background sweep.", sessionId);
            }
        }
    }
}
