using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterRetentionServiceTests
{
    private static readonly DateTime _nowUtc = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PurgeAsync_DrainsAnEntityUntilItIsEmpty()
    {
        // Arrange
        var policy = new FakeRetentionPolicy("Interaction", [10, 10, 10, 4]);

        var service = CreateService([policy], new ContactCenterRetentionOptions
        {
            InteractionRetentionDays = 30,
            PurgeBatchSize = 10,
        });

        // Act
        var report = await service.PurgeAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(34, report.TotalPurged);
        Assert.False(report.WorkRemains);
        Assert.Equal(4, policy.BatchCalls);
    }

    [Fact]
    public async Task PurgeAsync_WhenTheCycleBudgetRunsOut_ReportsThatWorkRemains()
    {
        // Arrange. The entity never drains, so only the budget can stop the cycle.
        var policy = new FakeRetentionPolicy("Interaction", [10, 10, 10, 10, 10, 10]);

        var service = CreateService([policy], new ContactCenterRetentionOptions
        {
            InteractionRetentionDays = 30,
            PurgeBatchSize = 10,
            MaxPurgeBatchesPerCycle = 3,
        });

        // Act
        var report = await service.PurgeAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(30, report.TotalPurged);
        Assert.Equal(3, policy.BatchCalls);
        Assert.True(report.WorkRemains);
        Assert.True(Assert.Single(report.Entities).WorkRemains);
    }

    [Fact]
    public async Task PurgeAsync_WhenAnEntityIsDisabled_ReportsItInsteadOfOmittingIt()
    {
        // Arrange
        var policy = new FakeRetentionPolicy("Interaction", [10]);

        var service = CreateService([policy], new ContactCenterRetentionOptions
        {
            InteractionRetentionDays = 0,
        });

        // Act
        var report = await service.PurgeAsync(TestContext.Current.CancellationToken);

        // Assert
        var entity = Assert.Single(report.Entities);

        Assert.Equal("Interaction", entity.EntityName);
        Assert.False(entity.IsEnabled);
        Assert.Null(entity.CutoffUtc);
        Assert.Equal(0, policy.BatchCalls);
        Assert.False(report.WorkRemains);
    }

    [Fact]
    public async Task PurgeAsync_WhenOneEntityFails_StillDrainsTheOthers()
    {
        // Arrange
        var failing = new FakeRetentionPolicy("Interaction", [10]) { Throws = true };
        var healthy = new FakeRetentionPolicy("CallSession", [3]);

        var service = CreateService([failing, healthy], new ContactCenterRetentionOptions
        {
            InteractionRetentionDays = 30,
            CallSessionRetentionDays = 30,
            PurgeBatchSize = 10,
        });

        // Act
        var report = await service.PurgeAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, report.TotalPurged);
        Assert.True(report.Entities.Single(entity => entity.EntityName == "Interaction").WorkRemains);
        Assert.False(report.Entities.Single(entity => entity.EntityName == "CallSession").WorkRemains);
    }

    [Fact]
    public async Task PurgeAsync_WhenABatchFailsPartwayThrough_DiscardsTheEntireBatchSoNoPartialWorkIsCommitted()
    {
        // Arrange
        // Deletes and prepare side effects are staged into a session shared by every entity, so a batch that fails
        // after staging some of them cannot commit only the completed records. Committing the partial state would
        // flush the failing record's half-staged side effects (for example an erased event with no outbox message),
        // so the whole batch is discarded with a session reset and retried on the next cycle instead.
        var failing = new FakeRetentionPolicy("Interaction", [10])
        {
            Throws = true,
            StagesBeforeThrowing = 4,
        };

        var healthy = new FakeRetentionPolicy("CallSession", [3]);
        var session = new Mock<ISession>();

        var service = CreateService([failing, healthy], new ContactCenterRetentionOptions
        {
            InteractionRetentionDays = 30,
            CallSessionRetentionDays = 30,
            PurgeBatchSize = 10,
        },
        session);

        // Act
        var report = await service.PurgeAsync(TestContext.Current.CancellationToken);

        // Assert
        var interactions = report.Entities.Single(entity => entity.EntityName == "Interaction");

        Assert.Equal(0, interactions.PurgedCount);
        Assert.True(interactions.WorkRemains);
        Assert.Equal(3, report.TotalPurged);
        Assert.Equal(1, failing.BatchCalls);
        Assert.False(report.Entities.Single(entity => entity.EntityName == "CallSession").WorkRemains);

        session.Verify(s => s.ResetAsync(), Times.Once);
    }

    [Fact]
    public async Task PurgeAsync_CommitsAfterEveryNonEmptyBatch_SoOneDrainIsNotOneUnboundedTransaction()
    {
        // Arrange
        var policy = new FakeRetentionPolicy("Interaction", [10, 10, 2]);
        var session = new Mock<ISession>();

        var service = CreateService([policy], new ContactCenterRetentionOptions
        {
            InteractionRetentionDays = 30,
            PurgeBatchSize = 10,
        },
        session);

        // Act
        await service.PurgeAsync(TestContext.Current.CancellationToken);

        // Assert
        session.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task PurgeAsync_WhenCanceled_ReportsThatWorkRemainsInsteadOfLookingSuccessful()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();

        var policy = new FakeRetentionPolicy("Interaction", [10, 10, 10])
        {
            OnBatch = () => cancellation.Cancel(),
        };

        var service = CreateService([policy], new ContactCenterRetentionOptions
        {
            InteractionRetentionDays = 30,
            PurgeBatchSize = 10,
        });

        // Act
        var report = await service.PurgeAsync(cancellation.Token);

        // Assert
        Assert.Equal(10, report.TotalPurged);
        Assert.True(report.WorkRemains);
    }

    [Fact]
    public async Task PurgeAsync_AppliesEachEntitysOwnCutoff()
    {
        // Arrange
        var interactions = new FakeRetentionPolicy("Interaction", [0]);
        var callSessions = new FakeRetentionPolicy("CallSession", [0]);

        var service = CreateService([interactions, callSessions], new ContactCenterRetentionOptions
        {
            InteractionRetentionDays = 30,
            CallSessionRetentionDays = 7,
        });

        // Act
        var report = await service.PurgeAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_nowUtc.AddDays(-30), report.Entities.Single(entity => entity.EntityName == "Interaction").CutoffUtc);
        Assert.Equal(_nowUtc.AddDays(-7), report.Entities.Single(entity => entity.EntityName == "CallSession").CutoffUtc);
    }

    private static ContactCenterRetentionService CreateService(
        IEnumerable<IContactCenterRetentionPolicy> policies,
        ContactCenterRetentionOptions options,
        Mock<ISession> session = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_nowUtc);

        return new ContactCenterRetentionService(
            policies,
            (session ?? new Mock<ISession>()).Object,
            clock.Object,
            Options.Create(options),
            NullLogger<ContactCenterRetentionService>.Instance);
    }

    private sealed class FakeRetentionPolicy : IContactCenterRetentionPolicy
    {
        private readonly int[] _batches;

        public FakeRetentionPolicy(string entityName, int[] batches)
        {
            EntityName = entityName;
            _batches = batches;
        }

        public string EntityName { get; }

        public Type IndexType => typeof(object);

        public Type ModelType => typeof(object);

        public int BatchCalls { get; private set; }

        public bool Throws { get; init; }

        public int StagesBeforeThrowing { get; init; }

        public Action OnBatch { get; init; }

        public bool TryGetCutoff(DateTime nowUtc, ContactCenterRetentionOptions options, out DateTime cutoffUtc)
        {
            var days = EntityName == "Interaction"
                ? options.InteractionRetentionDays
                : options.CallSessionRetentionDays;

            return RetentionCutoffCalculator.TryComputeCutoff(nowUtc, days, 0, out cutoffUtc);
        }

        public LambdaExpression GetExpiredPredicate(DateTime cutoffUtc)
            => (Expression<Func<DateTime, bool>>)(value => value < cutoffUtc);

        public Task<int> PurgeBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            if (Throws)
            {
                BatchCalls++;

                if (StagesBeforeThrowing > 0)
                {
                    throw new ContactCenterRetentionBatchException(EntityName, StagesBeforeThrowing, new InvalidOperationException("The entity is unhealthy."));
                }

                throw new InvalidOperationException("The entity is unhealthy.");
            }

            var purged = BatchCalls < _batches.Length ? _batches[BatchCalls] : 0;

            BatchCalls++;
            OnBatch?.Invoke();

            return Task.FromResult(purged);
        }
    }
}
