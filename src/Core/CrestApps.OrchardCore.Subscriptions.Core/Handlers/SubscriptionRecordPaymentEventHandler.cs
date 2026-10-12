using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Applies what a payment gateway reports about a recurring agreement to the durable
/// <see cref="Subscription"/> it belongs to.
/// </summary>
/// <remarks>
/// The gateway is the only thing that actually knows whether last night's renewal collected, whether the
/// customer canceled from the gateway's own dashboard, and when the next cycle falls. Without this bridge
/// the local record drifts: a subscription whose card failed keeps looking active, and one the customer
/// canceled at the gateway keeps granting access.
///
/// Every event is correlated by the provider's own subscription id, which is stored on the record and
/// indexed. Correlating any other way, for example through a cache, would silently drop events after a
/// restart.
/// </remarks>
public sealed class SubscriptionRecordPaymentEventHandler : PaymentEventBase
{
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRecordPaymentEventHandler"/> class.
    /// </summary>
    /// <param name="subscriptionManager">The subscription manager used to correlate the event.</param>
    /// <param name="lifecycleService">The service that owns every subscription transition.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SubscriptionRecordPaymentEventHandler(
        ISubscriptionManager subscriptionManager,
        ISubscriptionLifecycleService lifecycleService,
        IClock clock,
        ILogger<SubscriptionRecordPaymentEventHandler> logger)
    {
        _subscriptionManager = subscriptionManager;
        _lifecycleService = lifecycleService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public override async Task PaymentSucceededAsync(PaymentSucceededContext context, CancellationToken cancellationToken = default)
    {
        // Only a renewal advances the agreement. The first payment is what the checkout created it from, and
        // a mid-cycle update is a change of price rather than a new period.
        if (context?.Reason != PaymentReason.SubscriptionCycle)
        {
            return;
        }

        var subscription = await ResolveAsync(context.Subscription?.SubscriptionId, cancellationToken);

        if (subscription is null)
        {
            return;
        }

        // The gateway names the period it billed. Falling back to the agreement's own next billing date
        // keeps a notification that omitted it from being ignored, at the cost of trusting the local schedule.
        var periodStart = context.Subscription?.PeriodStartUtc
            ?? subscription.NextBillingUtc
            ?? subscription.CurrentPeriodEndUtc;

        await _lifecycleService.RecordRenewalAsync(
            subscription.ItemId,
            periodStart,
            new SubscriptionRenewalContext
            {
                TransactionId = context.TransactionId,
                AmountPaid = context.AmountPaid,
                PeriodEndUtc = context.Subscription?.PeriodEndUtc,
            },
            cancellationToken);
    }

    public override async Task SubscriptionPaymentFailedAsync(SubscriptionPaymentFailedContext context, CancellationToken cancellationToken = default)
    {
        var subscription = await ResolveAsync(context?.SubscriptionId, cancellationToken);

        if (subscription is null)
        {
            return;
        }

        // A gateway that is still retrying has not given up on the cycle, but the customer's payment has
        // already failed, so dunning starts now rather than after the last retry. Waiting would leave the
        // site owner unaware until access was about to be cut off.
        await _lifecycleService.MarkPastDueAsync(
            subscription.ItemId,
            context.FailureReason ?? context.FailureCode ?? "A renewal payment failed at the payment provider.",
            cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task SubscriptionStatusChangedAsync(SubscriptionStatusChangedContext context, CancellationToken cancellationToken = default)
    {
        var subscription = await ResolveAsync(context?.SubscriptionId, cancellationToken);

        if (subscription is null)
        {
            return;
        }

        if (context.Status is RemoteSubscriptionStatus.Canceled or RemoteSubscriptionStatus.IncompleteExpired)
        {
            // The gateway will not bill again. Honoring the period the customer paid for is still correct,
            // so the cancellation is applied at period end whenever the gateway says access runs on.
            var atPeriodEnd = context.CancelAtPeriodEnd ||
                (context.CurrentPeriodEndUtc.HasValue && context.CurrentPeriodEndUtc.Value > _clock.UtcNow);

            await _lifecycleService.CancelAsync(
                subscription.ItemId,
                atPeriodEnd,
                "The subscription was canceled at the payment provider.",
                context.GatewayId,
                cancellationToken);

            return;
        }

        var status = MapStatus(context.Status);

        await _lifecycleService.SyncFromProviderAsync(
            subscription.ItemId,
            new SubscriptionProviderSyncContext
            {
                Status = status,
                CurrentPeriodEndUtc = context.CurrentPeriodEndUtc,
                CancelAtPeriodEnd = context.CancelAtPeriodEnd,
                Source = context.GatewayId,
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context, CancellationToken cancellationToken = default)
    {
        // A gateway may report the agreement before the checkout that created it has finished writing the
        // record. Nothing needs to happen here beyond logging: the checkout owns creation, and creating a
        // second agreement from the notification would double the customer's subscriptions.
        if (context is not null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("The payment provider reported subscription '{SubscriptionId}'.", context.SubscriptionId);
        }

        await Task.CompletedTask;
    }

    private async Task<Subscription> ResolveAsync(string providerSubscriptionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(providerSubscriptionId))
        {
            return null;
        }

        var subscription = await _subscriptionManager.GetByProviderSubscriptionIdAsync(providerSubscriptionId, cancellationToken);

        if (subscription is null && _logger.IsEnabled(LogLevel.Debug))
        {
            // A tenant can legitimately receive events for agreements it does not own, for example when a
            // Stripe account is shared. Silence is correct; a warning would be noise.
            _logger.LogDebug("No local subscription matches provider subscription '{ProviderSubscriptionId}'.", providerSubscriptionId);
        }

        return subscription;
    }

    private static SubscriptionStatus? MapStatus(RemoteSubscriptionStatus status)
        => status switch
        {
            RemoteSubscriptionStatus.Active => SubscriptionStatus.Active,
            RemoteSubscriptionStatus.Trialing => SubscriptionStatus.Trialing,
            RemoteSubscriptionStatus.PastDue or RemoteSubscriptionStatus.Unpaid => SubscriptionStatus.PastDue,
            RemoteSubscriptionStatus.Paused => SubscriptionStatus.Paused,
            RemoteSubscriptionStatus.Incomplete => SubscriptionStatus.Incomplete,

            // Unknown means the gateway reported something this application does not model. Guessing a
            // status from it would be worse than leaving the record alone.
            _ => null,
        };
}
