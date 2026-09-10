using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the opt-in node serving gate.
/// </summary>
/// <remarks>
/// The gate exists because "shared dependency" does not mean "fails identically on every node": an exhausted
/// connection pool or a stale DNS entry breaks one node while its peers stay healthy. It is nonetheless unsafe
/// to enable by default, because a genuine store outage is observed by every node. These tests pin both halves
/// of that contract — it is free and silent when disabled, and it drains only after sustained local failure.
/// </remarks>
public sealed class ContactCenterNodeServingHealthCheckTests
{
    [Fact]
    public async Task Disabled_ReportsHealthyAndPerformsNoQuery()
    {
        // Readiness is probed at orchestrator frequency, so the default configuration must cost nothing.
        var store = CreateStore(() => throw new InvalidOperationException("the store must not be queried"));

        var check = CreateCheck(store.Object, enabled: false);

        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);

        store.Verify(
            s => s.CountByStatusAsync(It.IsAny<OutboxMessageStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Enabled_ReportsHealthy_WhenTheStoreIsReachable()
    {
        var store = CreateStore(() => 0);

        var check = CreateCheck(store.Object, enabled: true);

        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);

        store.Verify(
            s => s.CountByStatusAsync(It.IsAny<OutboxMessageStatus>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Enabled_KeepsServing_ThroughATransientFailure()
    {
        // One failed probe must never cost capacity.
        var store = CreateStore(() => throw new InvalidOperationException("transient"));

        var check = CreateCheck(store.Object, enabled: true, failuresBeforeUnready: 3);

        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Enabled_Drains_AfterConsecutiveFailures()
    {
        var store = CreateStore(() => throw new InvalidOperationException("connection pool exhausted"));

        var check = CreateCheck(store.Object, enabled: true, failuresBeforeUnready: 2);

        await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Enabled_Recovers_AfterConsecutiveSuccesses()
    {
        var shouldFail = true;

        var store = CreateStore(() => shouldFail
            ? throw new InvalidOperationException("connection pool exhausted")
            : 0);

        var check = CreateCheck(store.Object, enabled: true, failuresBeforeUnready: 1, successesBeforeReady: 2);

        var drained = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        shouldFail = false;

        var firstRecovery = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);
        var secondRecovery = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, drained.Status);
        Assert.Equal(HealthStatus.Unhealthy, firstRecovery.Status);
        Assert.Equal(HealthStatus.Healthy, secondRecovery.Status);
    }

    [Fact]
    public async Task Enabled_ReportsTheConfiguredFailureStatus()
    {
        // The registration decides the severity, so the check must not hard-code Unhealthy.
        var store = CreateStore(() => throw new InvalidOperationException("connection pool exhausted"));

        var check = CreateCheck(store.Object, enabled: true, failuresBeforeUnready: 1);

        var result = await check.CheckHealthAsync(
            CreateContext(HealthStatus.Degraded),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task ACancelledProbe_DoesNotCountAsAFailure()
    {
        // Shutdown or a client abort says nothing about this node's health, and must not drain it.
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        var store = CreateStore(() => throw new OperationCanceledException(cancellation.Token));

        var check = CreateCheck(store.Object, enabled: true, failuresBeforeUnready: 2);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.CheckHealthAsync(CreateContext(), cancellation.Token));

        // Had the cancellation been recorded as a failure, this second failure would drain the node.
        var afterOneRealFailure = await check.CheckHealthAsync(
            CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, afterOneRealFailure.Status);
    }

    [Fact]
    public async Task Enabled_ProbesACheapCompletedCount()
    {
        // The probe must stay cheap enough to run on every readiness scrape.
        var store = CreateStore(() => 0);

        var check = CreateCheck(store.Object, enabled: true);

        await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        store.Verify(
            s => s.CountByStatusAsync(OutboxMessageStatus.Completed, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IContactCenterOutboxStore> CreateStore(Func<int> count)
    {
        var store = new Mock<IContactCenterOutboxStore>();

        store
            .Setup(s => s.CountByStatusAsync(It.IsAny<OutboxMessageStatus>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(count()));

        return store;
    }

    private static ContactCenterNodeServingHealthCheck CreateCheck(
        IContactCenterOutboxStore store,
        bool enabled,
        int failuresBeforeUnready = 3,
        int successesBeforeReady = 2)
    {
        var options = new ContactCenterHealthCheckOptions
        {
            EnableNodeServingGate = enabled,
            ConsecutiveFailuresBeforeUnready = failuresBeforeUnready,
            ConsecutiveSuccessesBeforeReady = successesBeforeReady,
        };

        return new ContactCenterNodeServingHealthCheck(
            store,
            new NodeServingStateTracker(failuresBeforeUnready, successesBeforeReady),
            Options.Create(options));
    }

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy)
        => new()
        {
            Registration = new HealthCheckRegistration(
                "contactcenter-node-serving",
                _ => new NoopHealthCheck(),
                failureStatus,
                tags: null),
        };

    private sealed class NoopHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(HealthCheckResult.Healthy());
    }
}
