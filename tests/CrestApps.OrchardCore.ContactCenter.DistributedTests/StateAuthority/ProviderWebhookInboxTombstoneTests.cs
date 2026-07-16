using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.DistributedTests.Infrastructure;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.StateAuthority;

public sealed class ProviderWebhookInboxTombstoneTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptAsync_AfterSuccessfulDispatch_ReturnsDuplicateAndDoesNotReprocess()
    {
        // Arrange
        await using var harness = await ContactCenterStoreTestHarness.CreateAsync(
            "webhook-tombstone",
            [new ProviderWebhookInboxMessageIndexProvider(new ProviderIdentityResolver([]))],
            ContactCenterStoreTestHarness.CreateInboxSchemaAsync);
        await using var session = harness.Store.CreateSession();
        var handler = new CountingWebhookHandler();
        var scopeExecutor = new SameScopeExecutor();
        var inbox = CreateInbox(session, scopeExecutor, handler);
        scopeExecutor.Context = inbox;
        var accepted = await inbox.AcceptAsync(CreateDelivery(), TestContext.Current.CancellationToken);
        Assert.Equal(ProviderWebhookInboxAcceptanceStatus.Accepted, accepted.Status);
        Assert.True(await inbox.DispatchAsync(accepted.MessageId, TestContext.Current.CancellationToken));

        // Act
        var duplicate = await inbox.AcceptAsync(CreateDelivery(), TestContext.Current.CancellationToken);
        var processedAgain = await inbox.DispatchDueAsync(TestContext.Current.CancellationToken);
        var tombstone = await new ProviderWebhookInboxStore(session).FindByDeliveryAsync(
            "provider",
            "delivery-1",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderWebhookInboxAcceptanceStatus.Duplicate, duplicate.Status);
        Assert.Equal(accepted.MessageId, duplicate.MessageId);
        Assert.Equal(0, processedAgain);
        Assert.Equal(1, handler.HandledCount);
        Assert.NotNull(tombstone);
        Assert.Equal(ProviderWebhookInboxStatus.Completed, tombstone.Status);
        Assert.Equal(_now, tombstone.ProcessedUtc);
        Assert.Null(tombstone.Payload);
    }

    private static ProviderWebhookInbox CreateInbox(
        ISession session,
        SameScopeExecutor scopeExecutor,
        CountingWebhookHandler handler)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        return new ProviderWebhookInbox(
            [handler],
            new ProviderWebhookInboxStore(session),
            session,
            CreateDistributedLock(),
            new ProviderIdentityResolver([]),
            scopeExecutor,
            clock.Object,
            NullLogger<ProviderWebhookInbox>.Instance);
    }

    private static IDistributedLock CreateDistributedLock()
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(service => service.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return distributedLock.Object;
    }

    private static ProviderWebhookInboxDelivery CreateDelivery()
    {
        return new ProviderWebhookInboxDelivery
        {
            ProviderName = "provider",
            DeliveryId = "delivery-1",
            HandlerName = CountingWebhookHandler.Name,
            Payload = "{}",
        };
    }

    private sealed class CountingWebhookHandler : IProviderWebhookInboxHandler
    {
        public const string Name = "counting-handler";

        public int HandledCount { get; private set; }

        public string TechnicalName => Name;

        public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

        public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
        {
            HandledCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class SameScopeExecutor : IContactCenterScopeExecutor
    {
        public IProviderWebhookInbox Context { get; set; }

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
        {
            return operation((TContext)Context);
        }

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
        {
            return false;
        }

        public bool ScheduleAfterCommit(Func<Task> operation)
        {
            return false;
        }
    }
}
