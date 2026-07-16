using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.DistributedTests.Infrastructure;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.StateAuthority;

public sealed class CallSessionTopologyStateAuthorityTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateAsync_SupervisorLegWithoutSupervisorAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var harness = await ContactCenterStoreTestHarness.CreateAsync(
            "call-topology-invariant",
            [new CallSessionIndexProvider(new ProviderIdentityResolver([]))],
            ContactCenterStoreTestHarness.CreateCallSessionSchemaAsync);
        await SeedCallSessionAsync(harness.Store);
        await using var session = harness.Store.CreateSession();
        var store = new CallSessionStore(session);
        var callSession = await store.FindByIdAsync("call-session-1", TestContext.Current.CancellationToken);
        callSession.SupervisorLegId = "supervisor-leg-1";

        // Act
        var exception = await Record.ExceptionAsync(() =>
            store.UpdateAsync(callSession, TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task UpdateAsync_TwoWorkersReadSameTopologyVersion_OnlyOneCommits()
    {
        // Arrange
        await using var harness = await ContactCenterStoreTestHarness.CreateAsync(
            "call-topology-cas",
            [new CallSessionIndexProvider(new ProviderIdentityResolver([]))],
            ContactCenterStoreTestHarness.CreateCallSessionSchemaAsync);
        await SeedCallSessionAsync(harness.Store);

        await using var firstSession = harness.Store.CreateSession();
        await using var secondSession = harness.Store.CreateSession();
        var firstStore = new CallSessionStore(firstSession);
        var secondStore = new CallSessionStore(secondSession);
        var firstCallSession = await firstStore.FindByIdAsync("call-session-1", TestContext.Current.CancellationToken);
        var secondCallSession = await secondStore.FindByIdAsync("call-session-1", TestContext.Current.CancellationToken);

        firstCallSession.BridgeId = "bridge-1";
        firstCallSession.DurableCommandId = "command-1";
        secondCallSession.BridgeId = "bridge-2";
        secondCallSession.DurableCommandId = "command-2";
        await firstStore.UpdateAsync(firstCallSession, TestContext.Current.CancellationToken);
        await secondStore.UpdateAsync(secondCallSession, TestContext.Current.CancellationToken);

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
    }

    private static async Task SeedCallSessionAsync(IStore store)
    {
        await using var session = store.CreateSession();
        await new CallSessionStore(session).CreateAsync(new CallSession
        {
            ItemId = "call-session-1",
            InteractionId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "provider",
            ProviderCallId = "provider-call-1",
            State = ContactCenterCallState.Connected,
            AgentId = "agent-1",
            AgentSessionId = "agent-session-1",
            QueueId = "queue-1",
            CreatedUtc = _now,
        }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
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
}
