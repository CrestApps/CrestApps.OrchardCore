using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.DistributedTests.Infrastructure;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.StateAuthority;

public sealed class InteractionStoreConcurrencyTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateAsync_TwoWorkersReadSameInteractionVersion_OnlyOneCommits()
    {
        // Arrange
        await using var harness = await ContactCenterStoreTestHarness.CreateAsync(
            "interaction-cas",
            [new InteractionIndexProvider()],
            ContactCenterStoreTestHarness.CreateInteractionSchemaAsync);
        await SeedInteractionAsync(harness.Store);

        await using var firstSession = harness.Store.CreateSession();
        await using var secondSession = harness.Store.CreateSession();
        var firstStore = new InteractionStore(firstSession);
        var secondStore = new InteractionStore(secondSession);
        var firstInteraction = await firstStore.FindByIdAsync("interaction-1", TestContext.Current.CancellationToken);
        var secondInteraction = await secondStore.FindByIdAsync("interaction-1", TestContext.Current.CancellationToken);

        firstInteraction.Status = InteractionStatus.Connected;
        secondInteraction.Status = InteractionStatus.Failed;
        await firstStore.UpdateAsync(firstInteraction, TestContext.Current.CancellationToken);
        await secondStore.UpdateAsync(secondInteraction, TestContext.Current.CancellationToken);

        // Act
        var attempts = await Task.WhenAll(
            CaptureAsync(firstSession),
            CaptureAsync(secondSession));

        // Assert
        Assert.Single(attempts, exception => exception is null);
        var failure = Assert.Single(attempts, exception => exception is not null);
        Assert.True(
            failure is ConcurrencyException or DbException,
            $"Expected an optimistic-concurrency failure but received {failure.GetType().Name}.");

        var persisted = await ReadInteractionAsync(harness.Store);
        Assert.NotEqual(InteractionStatus.Created, persisted.Status);
    }

    private static async Task<Exception> CaptureAsync(ISession session)
    {
        try
        {
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task SeedInteractionAsync(IStore store)
    {
        await using var session = store.CreateSession();
        await new InteractionStore(session).CreateAsync(new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            Status = InteractionStatus.Created,
            CreatedUtc = _now,
        }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Interaction> ReadInteractionAsync(IStore store)
    {
        await using var session = store.CreateSession();

        return await new InteractionStore(session).FindByIdAsync("interaction-1", TestContext.Current.CancellationToken);
    }
}
