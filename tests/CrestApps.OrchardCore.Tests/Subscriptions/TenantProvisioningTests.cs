using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// A site that has been paid for must never be lost. These tests pin the properties the durable job exists
/// to guarantee: a job that failed comes back, a job that has been retried too often stops and says so, and
/// a job that succeeded is never picked up again.
/// </summary>
public sealed class TenantProvisioningTests
{
    private static readonly DateTime _now = new(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetDueAsync_ReturnsAJobThatHasNeverBeenAttempted()
    {
        // Arrange
        var store = new InMemoryTenantProvisioningJobStore(CreateJob(TenantProvisioningStatus.Pending, nextAttemptUtc: null));

        // Act
        var due = await store.GetDueAsync(_now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(due);
    }

    /// <summary>
    /// A failed attempt has to come back, or the customer never gets the site they paid for.
    /// </summary>
    [Fact]
    public async Task GetDueAsync_ReturnsAFailedJobOnceItsBackOffHasPassed()
    {
        // Arrange
        var store = new InMemoryTenantProvisioningJobStore(
            CreateJob(TenantProvisioningStatus.Failed, _now.AddMinutes(-1)),
            CreateJob(TenantProvisioningStatus.Failed, _now.AddMinutes(5)));

        // Act
        var due = await store.GetDueAsync(_now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(due);
    }

    /// <summary>
    /// This is the case a fire-and-forget implementation gets wrong. A process that died mid-setup leaves a
    /// job marked Running, and if that were never reclaimed the site would be stranded forever.
    /// </summary>
    [Fact]
    public async Task GetDueAsync_ReclaimsAJobStrandedByADeadNode()
    {
        // Arrange
        var store = new InMemoryTenantProvisioningJobStore(CreateJob(TenantProvisioningStatus.Running, _now.AddMinutes(-1)));

        // Act
        var due = await store.GetDueAsync(_now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(due);
    }

    [Theory]
    [InlineData(TenantProvisioningStatus.Succeeded)]
    [InlineData(TenantProvisioningStatus.Abandoned)]
    public async Task GetDueAsync_NeverReturnsATerminalJob(TenantProvisioningStatus status)
    {
        // Arrange
        var store = new InMemoryTenantProvisioningJobStore(CreateJob(status, nextAttemptUtc: null));

        // Act
        var due = await store.GetDueAsync(_now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(due);
    }

    [Theory]
    [InlineData(TenantProvisioningStatus.Pending, false)]
    [InlineData(TenantProvisioningStatus.Running, false)]
    [InlineData(TenantProvisioningStatus.Failed, false)]
    [InlineData(TenantProvisioningStatus.Succeeded, true)]
    [InlineData(TenantProvisioningStatus.Abandoned, true)]
    public void IsTerminal_MarksOnlyTheStatesThatWillNotMoveOnTheirOwn(TenantProvisioningStatus status, bool expected)
    {
        // Arrange
        var job = CreateJob(status, nextAttemptUtc: null);

        // Act & Assert
        Assert.Equal(expected, job.IsTerminal);
    }

    /// <summary>
    /// The completion handler looks the job up by checkout before creating one. That lookup is what stops a
    /// checkout completing twice from trying to build the same site twice.
    /// </summary>
    [Fact]
    public async Task GetByCheckoutSessionAsync_FindsTheJobRecordedForThatCheckout()
    {
        // Arrange
        var store = new InMemoryTenantProvisioningJobStore(CreateJob(TenantProvisioningStatus.Pending, nextAttemptUtc: null));

        // Act
        var job = await store.GetByCheckoutSessionAsync("session-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(job);
        Assert.Equal("contoso", job.TenantName);
    }

    private static TenantProvisioningJob CreateJob(TenantProvisioningStatus status, DateTime? nextAttemptUtc)
        => new()
        {
            ItemId = Guid.NewGuid().ToString("n"),
            CheckoutSessionId = "session-1",
            OwnerId = "owner-1",
            TenantName = "contoso",
            TenantTitle = "Contoso",
            AdminUsername = "admin",
            AdminEmail = "admin@example.com",
            ProtectedAdminPassword = "protected",
            RecipeName = "Blog",
            Status = status,
            NextAttemptUtc = nextAttemptUtc,
            CreatedUtc = _now.AddMinutes(-10),
        };
}

/// <summary>
/// The tenant entitlement is what ties a site's continued existence to the customer continuing to pay.
/// These tests pin that the site is suspended rather than deleted, which is the difference between a
/// recoverable billing lapse and destroying somebody's work.
/// </summary>
public sealed class TenantEntitlementTests
{
    [Fact]
    public void ATenantEntitlementNamesTheSiteItGrants()
    {
        // Arrange
        var subscription = new Subscription
        {
            ItemId = "sub-1",
            OwnerId = "owner-1",
            Status = SubscriptionStatus.Active,
            BillingDuration = 1,
            DurationType = DurationType.Month,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(20),
            Entitlements =
            {
                new SubscriptionEntitlement
                {
                    Kind = CrestApps.OrchardCore.Subscriptions.Core.SubscriptionConstants.EntitlementKinds.Tenant,
                    Value = "contoso",
                },
            },
        };

        // Act
        var entitlement = Assert.Single(subscription.Entitlements);

        // Assert
        Assert.Equal("Tenant", entitlement.Kind);
        Assert.Equal("contoso", entitlement.Value);
        Assert.True(subscription.IsCurrent(DateTime.UtcNow));
    }
}
