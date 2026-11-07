using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Canceling changes only what this site believes. The gateway holds the card and its own schedule, so
/// unless it is told, a subscriber who cancels keeps being charged every month while the site shows their
/// subscription as ended — the worst possible combination.
/// </summary>
/// <remarks>
/// This went unnoticed because <c>CancelRecurringAsync</c> was implemented by both providers and called by
/// nothing. Only cancelling a real Stripe agreement and reading it back showed the subscription still active.
/// </remarks>
public sealed class GatewayBillingSubscriptionLifecycleHandlerTests
{
    /// <summary>
    /// The gateway refuses to cancel: the customer may still be billed, so an operator must see it, not only the log.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenTheGatewayRefusesToCancel_RecordsAPaymentIssue()
    {
        var provider = new RecordingRecurringProvider { CancelError = "No such subscription." };
        var issues = new RecordingIssueRecorder();
        var handler = new GatewayBillingSubscriptionLifecycleHandler([provider], issues, NullLogger<GatewayBillingSubscriptionLifecycleHandler>.Instance);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Canceled, cancelAtPeriodEnd: true),
            SubscriptionStatus.Active,
            source: "customer"));

        var issue = Assert.Single(issues.Issues);

        Assert.Equal(PaymentIssueKinds.GatewaySubscriptionChangeFailed, issue.Kind);
        Assert.Equal(("Subscription", "subscription-1", "Stripe", "sub_1"), (issue.ReferenceType, issue.ReferenceId, issue.ProviderKey, issue.ProviderReference));
        Assert.Equal("No such subscription.", issue.Error);
    }

    /// <summary>
    /// A cancellation the gateway accepts needs no operator.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenTheGatewayCancels_RecordsNoIssue()
    {
        var issues = new RecordingIssueRecorder();
        var handler = new GatewayBillingSubscriptionLifecycleHandler([new RecordingRecurringProvider()], issues, NullLogger<GatewayBillingSubscriptionLifecycleHandler>.Instance);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Canceled, cancelAtPeriodEnd: true),
            SubscriptionStatus.Active,
            source: "customer"));

        Assert.Empty(issues.Issues);
    }

    /// <summary>
    /// The customer cancels; the gateway is told to stop billing.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenCanceledHere_CancelsAtTheGateway()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Canceled, cancelAtPeriodEnd: true),
            SubscriptionStatus.Active,
            source: "customer"));

        var context = Assert.Single(provider.Canceled);

        Assert.Equal("sub_1", context.ProviderSubscriptionId);
        Assert.True(context.AtPeriodEnd);
    }

    /// <summary>
    /// Cancelling immediately means immediately at the gateway too, or the customer keeps being billed for
    /// access they no longer have.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_CancelingImmediately_DoesNotDeferAtTheGateway()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Canceled, cancelAtPeriodEnd: false),
            SubscriptionStatus.Active));

        Assert.False(Assert.Single(provider.Canceled).AtPeriodEnd);
    }

    /// <summary>
    /// The gateway reported the cancellation. Sending it back would ask Stripe to cancel an agreement it has
    /// already canceled.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenTheGatewayReportedIt_SendsNothingBack()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Canceled, cancelAtPeriodEnd: true),
            SubscriptionStatus.Active,
            source: "Stripe"));

        Assert.Empty(provider.Canceled);
    }

    /// <summary>
    /// A later edit of an already-canceled agreement must not cancel it a second time.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenAlreadyCanceled_CancelsOnlyOnce()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Canceled, cancelAtPeriodEnd: true),
            SubscriptionStatus.Canceled));

        Assert.Empty(provider.Canceled);
    }

    /// <summary>
    /// Any other transition leaves the agreement billing.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_ForANonCancellation_SendsNothing()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.PastDue, cancelAtPeriodEnd: false),
            SubscriptionStatus.Active));

        Assert.Empty(provider.Canceled);
        Assert.Empty(provider.Collection);
    }

    /// <summary>
    /// An agreement that no gateway backs has nothing to tell.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WithoutAProviderSubscription_SendsNothing()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        var subscription = CreateSubscription(SubscriptionStatus.Canceled, cancelAtPeriodEnd: true);
        subscription.ProviderSubscriptionId = null;

        await handler.ChangedAsync(new SubscriptionLifecycleContext(subscription, SubscriptionStatus.Active));

        Assert.Empty(provider.Canceled);
    }

    /// <summary>
    /// The admin's "billing was suspended" has to be true at the gateway, not only here.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenPaused_SuspendsCollectionAtTheGateway()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Paused, cancelAtPeriodEnd: false),
            SubscriptionStatus.Active));

        Assert.True(Assert.Single(provider.Collection).Paused);
        Assert.Empty(provider.Canceled);
    }

    /// <summary>
    /// Resuming a suspended agreement puts it back on the gateway's schedule.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenResumedFromPaused_ResumesCollection()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Active, cancelAtPeriodEnd: false),
            SubscriptionStatus.Paused));

        Assert.False(Assert.Single(provider.Collection).Paused);
    }

    /// <summary>
    /// Clearing a past-due flag is a dunning decision. Collection was never suspended, so there is nothing
    /// at the gateway to resume — and telling it to resume would clear a suspension somebody else set.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenResumedFromPastDue_TouchesNothing()
    {
        var provider = new RecordingRecurringProvider();
        var handler = CreateHandler(provider);

        await handler.ChangedAsync(new SubscriptionLifecycleContext(
            CreateSubscription(SubscriptionStatus.Active, cancelAtPeriodEnd: false),
            SubscriptionStatus.PastDue));

        Assert.Empty(provider.Collection);
        Assert.Empty(provider.Canceled);
    }

    private static GatewayBillingSubscriptionLifecycleHandler CreateHandler(RecordingRecurringProvider provider)
        => new([provider], new RecordingIssueRecorder(), NullLogger<GatewayBillingSubscriptionLifecycleHandler>.Instance);

    private static Subscription CreateSubscription(SubscriptionStatus status, bool cancelAtPeriodEnd)
        => new()
        {
            ItemId = "subscription-1",
            OwnerId = "owner-1",
            Status = status,
            ProviderKey = "Stripe",
            ProviderSubscriptionId = "sub_1",
            CancelAtPeriodEnd = cancelAtPeriodEnd,
            CancellationReason = "Canceled by the subscriber.",
        };

    private sealed class RecordingRecurringProvider : ICheckoutRecurringPaymentProvider
    {
        public List<CancelRecurringPaymentContext> Canceled { get; } = [];

        public List<PauseRecurringPaymentContext> Collection { get; } = [];

        public string CancelError { get; set; }

        public string Key => "Stripe";

        public Task<PaymentBeginResult> BeginRecurringAsync(BeginRecurringPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentBeginResult { Succeeded = true });

        public Task<RecurringCancelResult> CancelRecurringAsync(CancelRecurringPaymentContext context, CancellationToken cancellationToken = default)
        {
            Canceled.Add(context);

            return Task.FromResult(CancelError is null ? RecurringCancelResult.Success(null) : RecurringCancelResult.Failure(CancelError));
        }

        public Task<RecurringPauseResult> PauseRecurringAsync(PauseRecurringPaymentContext context, CancellationToken cancellationToken = default)
        {
            Collection.Add(context);

            return Task.FromResult(RecurringPauseResult.Success());
        }

        public Task<RecurringUpdateResult> UpdateRecurringAsync(UpdateRecurringPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(RecurringUpdateResult.Failure("not used"));
    }

    private sealed class RecordingIssueRecorder : IPaymentIssueRecorder
    {
        public List<PaymentIssue> Issues { get; } = [];

        public Task<bool> RecordAsync(PaymentIssue issue, CancellationToken cancellationToken = default)
        {
            Issues.Add(issue);

            return Task.FromResult(true);
        }
    }
}
