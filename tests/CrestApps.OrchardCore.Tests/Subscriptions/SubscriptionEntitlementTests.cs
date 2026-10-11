using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Entitlements are what make a subscription mean something outside billing. These tests pin the two rules
/// that decide whether a paying customer is treated fairly: access lasts as long as the agreement is
/// current, not as long as its status says Active, and it genuinely goes away once it is not.
/// </summary>
public sealed class SubscriptionEntitlementTests
{
    private static readonly DateTime _now = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HasEntitlementAsync_ForAnActiveSubscription_IsTrue()
    {
        // Arrange
        var service = CreateAccessService(CreateSubscription(SubscriptionStatus.Active));

        // Act & Assert
        Assert.True(await service.HasEntitlementAsync("owner-1", SubscriptionConstants.EntitlementKinds.Role, "Member", TestContext.Current.CancellationToken));
        Assert.False(await service.HasEntitlementAsync("owner-1", SubscriptionConstants.EntitlementKinds.Role, "Admin", TestContext.Current.CancellationToken));
        Assert.True(await service.HasEntitlementAsync("owner-1", SubscriptionConstants.EntitlementKinds.Role, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A customer who cancelled mid-cycle keeps what they paid for, and one whose card failed keeps access
    /// through the grace window. Both would be locked out by a check that only looked at the status.
    /// </summary>
    [Fact]
    public async Task HasEntitlementAsync_ForACancelledButPaidSubscription_IsTrue()
    {
        // Arrange
        var cancelled = CreateSubscription(SubscriptionStatus.Canceled);

        cancelled.CurrentPeriodEndUtc = _now.AddDays(10);

        var service = CreateAccessService(cancelled);

        // Act & Assert
        Assert.True(await service.HasEntitlementAsync("owner-1", SubscriptionConstants.EntitlementKinds.Role, "Member", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HasEntitlementAsync_WithinTheGraceWindow_IsTrue()
    {
        // Arrange
        var pastDue = CreateSubscription(SubscriptionStatus.PastDue);

        pastDue.GraceEndsUtc = _now.AddDays(3);

        var service = CreateAccessService(pastDue);

        // Act & Assert
        Assert.True(await service.HasEntitlementAsync("owner-1", SubscriptionConstants.EntitlementKinds.Role, "Member", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(SubscriptionStatus.Expired)]
    [InlineData(SubscriptionStatus.Incomplete)]
    public async Task HasEntitlementAsync_ForAnEndedSubscription_IsFalse(SubscriptionStatus status)
    {
        // Arrange
        var ended = CreateSubscription(status);

        ended.CurrentPeriodEndUtc = _now.AddDays(-1);

        var service = CreateAccessService(ended);

        // Act & Assert
        Assert.False(await service.HasEntitlementAsync("owner-1", SubscriptionConstants.EntitlementKinds.Role, "Member", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChangedAsync_WhenTheSubscriptionIsCurrent_AppliesTheEntitlement()
    {
        // Arrange
        var applier = new RecordingApplier();
        var handler = CreateHandler(applier);
        var subscription = CreateSubscription(SubscriptionStatus.Active);

        // Act
        await handler.ChangedAsync(new SubscriptionLifecycleContext(subscription, SubscriptionStatus.Incomplete));

        // Assert
        Assert.Equal(["Member"], applier.Applied);
        Assert.Empty(applier.Revoked);
    }

    /// <summary>
    /// A subscriber who stops paying and keeps the role keeps everything they were paying for, which is the
    /// failure that costs a site owner money quietly and indefinitely.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenTheSubscriptionStopsBeingCurrent_RevokesTheEntitlement()
    {
        // Arrange
        var applier = new RecordingApplier();
        var handler = CreateHandler(applier);
        var subscription = CreateSubscription(SubscriptionStatus.Expired);

        subscription.CurrentPeriodEndUtc = _now.AddDays(-1);

        // Act
        await handler.ChangedAsync(new SubscriptionLifecycleContext(subscription, SubscriptionStatus.PastDue));

        // Assert
        Assert.Equal(["Member"], applier.Revoked);
        Assert.Empty(applier.Applied);
    }

    /// <summary>
    /// A kind whose feature is not enabled on the tenant is a configuration choice, not an error, and must
    /// not break the transition that triggered it.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WithNoApplierForTheKind_DoesNothing()
    {
        // Arrange
        var handler = CreateHandler();
        var subscription = CreateSubscription(SubscriptionStatus.Active);

        // Act & Assert (no exception)
        await handler.ChangedAsync(new SubscriptionLifecycleContext(subscription, SubscriptionStatus.Incomplete));
    }

    /// <summary>
    /// An applier that throws must not roll back the transition. The agreement's state is the fact; a role
    /// that could not be removed is a side effect to retry.
    /// </summary>
    [Fact]
    public async Task ChangedAsync_WhenAnApplierThrows_DoesNotPropagate()
    {
        // Arrange
        var handler = CreateHandler(new ThrowingApplier());
        var subscription = CreateSubscription(SubscriptionStatus.Active);

        // Act & Assert (no exception)
        await handler.ChangedAsync(new SubscriptionLifecycleContext(subscription, SubscriptionStatus.Incomplete));
    }

    private static Subscription CreateSubscription(SubscriptionStatus status)
        => new()
        {
            ItemId = "sub-1",
            Title = "Membership",
            OwnerId = "owner-1",
            Status = status,
            Currency = "USD",
            Amount = 20m,
            BillingDuration = 1,
            DurationType = DurationType.Month,
            CurrentPeriodStartUtc = _now.AddDays(-10),
            CurrentPeriodEndUtc = _now.AddDays(20),
            Entitlements =
            {
                new SubscriptionEntitlement { Kind = SubscriptionConstants.EntitlementKinds.Role, Value = "Member" },
            },
        };

    private static DefaultSubscriptionAccessService CreateAccessService(Subscription seed)
    {
        var manager = new SubscriptionManager(
            new InMemorySubscriptionStore(seed),
            [],
            NullLogger<CatalogManager<Subscription>>.Instance);

        return new DefaultSubscriptionAccessService(manager, new TestClock(_now));
    }

    private static EntitlementSubscriptionLifecycleHandler CreateHandler(params ISubscriptionEntitlementApplier[] appliers)
        => new(appliers, new TestClock(_now), NullLogger<EntitlementSubscriptionLifecycleHandler>.Instance);

    private sealed class RecordingApplier : ISubscriptionEntitlementApplier
    {
        public string Kind => SubscriptionConstants.EntitlementKinds.Role;

        public List<string> Applied { get; } = [];

        public List<string> Revoked { get; } = [];

        public Task ApplyAsync(SubscriptionEntitlementContext context)
        {
            Applied.Add(context.Entitlement.Value);

            return Task.CompletedTask;
        }

        public Task RevokeAsync(SubscriptionEntitlementContext context)
        {
            Revoked.Add(context.Entitlement.Value);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingApplier : ISubscriptionEntitlementApplier
    {
        public string Kind => SubscriptionConstants.EntitlementKinds.Role;

        public Task ApplyAsync(SubscriptionEntitlementContext context)
            => throw new InvalidOperationException("The role store is unavailable.");

        public Task RevokeAsync(SubscriptionEntitlementContext context)
            => throw new InvalidOperationException("The role store is unavailable.");
    }
}
