using CrestApps.OrchardCore.Retention;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.BackgroundTasks;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Core.Retention;

/// <summary>
/// The platform retention runner drains every registered policy with its own batch budget, commits each batch and
/// reports what is left; modules register policies and the one retention task with <c>AddRetentionPolicy</c>.
/// </summary>
public sealed class RetentionRunnerTests
{
    private static readonly DateTime _now = new(2026, 11, 4, 3, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EveryPolicyDrains_WithItsOwnBudget()
    {
        // Arrange
        var session = new Mock<ISession>();
        var large = new FakePolicy("Large", remaining: 25);
        var small = new FakePolicy("Small", remaining: 3);
        var kept = new FakePolicy("Kept", remaining: 5, enabled: false);

        // Act
        var report = await RetentionRunner.RunAsync([large, small, kept], session.Object, _now, batchSize: 10, batchBudget: 2, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(20, report.Policies.Single(policy => policy.Name == "Large").PurgedCount);
        Assert.True(report.Policies.Single(policy => policy.Name == "Large").WorkRemains);
        Assert.Equal(3, report.Policies.Single(policy => policy.Name == "Small").PurgedCount);
        Assert.False(report.Policies.Single(policy => policy.Name == "Kept").IsEnabled);
        Assert.Equal(23, report.TotalPurged);
        session.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task AFailedBatch_IsDiscardedAndTheOthersStillDrain()
    {
        // Arrange
        var session = new Mock<ISession>();
        var broken = new FakePolicy("Broken", remaining: 5, fail: true);
        var healthy = new FakePolicy("Healthy", remaining: 4);

        // Act
        var report = await RetentionRunner.RunAsync([broken, healthy], session.Object, _now, batchSize: 10, batchBudget: 5, NullLogger.Instance, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(report.Policies.Single(policy => policy.Name == "Broken").WorkRemains);
        Assert.Equal(4, report.Policies.Single(policy => policy.Name == "Healthy").PurgedCount);
        session.Verify(value => value.ResetAsync(), Times.Once);
    }

    [Fact]
    public void AddRetentionPolicy_RegistersTheRetentionTaskOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRetentionPolicy<FirstPolicy>();
        services.AddRetentionPolicy<SecondPolicy>();

        // Assert
        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IRetentionPolicy)));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IBackgroundTask) && descriptor.ImplementationType == typeof(RetentionBackgroundTask));
    }

    private class FakePolicy : IRetentionPolicy
    {
        private readonly bool _enabled;
        private readonly bool _fail;
        private int _remaining;

        public FakePolicy(string name, int remaining, bool enabled = true, bool fail = false)
        {
            Name = name;
            _remaining = remaining;
            _enabled = enabled;
            _fail = fail;
        }

        public string Name { get; }

        public bool TryGetCutoff(DateTime nowUtc, out DateTime cutoffUtc)
        {
            cutoffUtc = nowUtc.AddDays(-30);

            return _enabled;
        }

        public Task<int> PurgeBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            if (_fail)
            {
                throw new RetentionBatchException(Name, 1, new InvalidOperationException("Unhealthy."));
            }

            var purged = Math.Min(batchSize, _remaining);
            _remaining -= purged;

            return Task.FromResult(purged);
        }
    }

    private sealed class FirstPolicy : FakePolicy
    {
        public FirstPolicy()
            : base("First", 0)
        {
        }
    }

    private sealed class SecondPolicy : FakePolicy
    {
        public SecondPolicy()
            : base("Second", 0)
        {
        }
    }
}
