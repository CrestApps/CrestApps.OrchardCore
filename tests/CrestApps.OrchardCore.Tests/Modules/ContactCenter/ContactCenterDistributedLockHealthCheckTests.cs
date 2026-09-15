using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the distributed-lock dependency probe.
/// </summary>
/// <remarks>
/// The probe proves the resolved lock can be taken and released. A production topology resolves a Redis-backed
/// lock, so a failure here is the early warning that the lock serializing overlapping processes during a
/// rolling restart is down.
/// </remarks>
public sealed class ContactCenterDistributedLockHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReportsHealthy_WhenTheLockIsAcquiredAndReleased()
    {
        // Arrange
        var distributedLock = new FakeDistributedLock(acquired: true);
        var check = new ContactCenterDistributedLockHealthCheck(distributedLock);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(distributedLock.Released);
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsFailureStatus_WhenTheLockCannotBeAcquired()
    {
        // Arrange
        var distributedLock = new FakeDistributedLock(acquired: false);
        var check = new ContactCenterDistributedLockHealthCheck(distributedLock);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(HealthStatus.Degraded), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsFailureStatus_WhenTheLockBackendThrows()
    {
        // Arrange
        var distributedLock = new FakeDistributedLock(acquired: true, throwOnAcquire: true);
        var check = new ContactCenterDistributedLockHealthCheck(distributedLock);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy)
        => new()
        {
            Registration = new HealthCheckRegistration(
                ContactCenterConstants.HealthChecks.DistributedLockCheckName,
                _ => throw new NotSupportedException("The registration factory is not used by these tests."),
                failureStatus,
                tags: null),
        };

    private sealed class FakeDistributedLock : IDistributedLock
    {
        private readonly bool _acquired;
        private readonly bool _throwOnAcquire;

        public FakeDistributedLock(bool acquired, bool throwOnAcquire = false)
        {
            _acquired = acquired;
            _throwOnAcquire = throwOnAcquire;
        }

        public bool Released { get; private set; }

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            if (_throwOnAcquire)
            {
                throw new InvalidOperationException("The lock backend is unreachable.");
            }

            return Task.FromResult<(ILocker, bool)>((new FakeLocker(this), _acquired));
        }

        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => Task.FromResult<ILocker>(new FakeLocker(this));

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(false);

        private sealed class FakeLocker : ILocker
        {
            private readonly FakeDistributedLock _owner;

            public FakeLocker(FakeDistributedLock owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                _owner.Released = true;
            }

            public ValueTask DisposeAsync()
            {
                _owner.Released = true;

                return ValueTask.CompletedTask;
            }
        }
    }
}
