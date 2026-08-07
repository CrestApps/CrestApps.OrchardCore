using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.DistributedTests.Infrastructure;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.StateAuthority;

public sealed class AgentSessionStateAuthorityTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_TwoOnlineSessionsForSameUser_OnlyOneCommits()
    {
        // Arrange
        await using var harness = await ContactCenterStoreTestHarness.CreateAsync(
            "agent-online-claim",
            [new AgentSessionIndexProvider()],
            ContactCenterStoreTestHarness.CreateAgentSessionSchemaAsync);
        await using var firstSession = harness.Store.CreateSession();
        await using var secondSession = harness.Store.CreateSession();
        await new AgentSessionStore(firstSession).CreateAsync(CreateSession("agent-session-1"), TestContext.Current.CancellationToken);
        await new AgentSessionStore(secondSession).CreateAsync(CreateSession("agent-session-2"), TestContext.Current.CancellationToken);

        // Act
        var attempts = await Task.WhenAll(
            CaptureAsync(firstSession),
            CaptureAsync(secondSession));

        // Assert
        Assert.Single(attempts, exception => exception is null);
        var failure = Assert.Single(attempts, exception => exception is not null);
        Assert.True(
            failure is DbException or ConcurrencyException,
            $"Expected a unique-claim or concurrency failure but received {failure.GetType().Name}.");

        await using var querySession = harness.Store.CreateSession();
        var online = await new AgentSessionStore(querySession).ListByUserIdsAsync(["user-1"], TestContext.Current.CancellationToken);
        Assert.Single(online, session => session.IsOnline);
    }

    [Fact]
    public async Task UpdateAsync_TwoWorkersReadSameAgentSessionVersion_OnlyOneCommits()
    {
        // Arrange
        await using var harness = await ContactCenterStoreTestHarness.CreateAsync(
            "agent-session-cas",
            [new AgentSessionIndexProvider()],
            ContactCenterStoreTestHarness.CreateAgentSessionSchemaAsync);
        await SeedSessionAsync(harness.Store);

        await using var firstSession = harness.Store.CreateSession();
        await using var secondSession = harness.Store.CreateSession();
        var firstStore = new AgentSessionStore(firstSession);
        var secondStore = new AgentSessionStore(secondSession);
        var firstAgentSession = await firstStore.FindByIdAsync("agent-session-1", TestContext.Current.CancellationToken);
        var secondAgentSession = await secondStore.FindByIdAsync("agent-session-1", TestContext.Current.CancellationToken);

        firstAgentSession.LastHeartbeatUtc = _now.AddSeconds(1);
        secondAgentSession.LastHeartbeatUtc = _now.AddSeconds(2);
        await firstStore.UpdateAsync(firstAgentSession, TestContext.Current.CancellationToken);
        await secondStore.UpdateAsync(secondAgentSession, TestContext.Current.CancellationToken);

        // Act
        var attempts = await Task.WhenAll(
            CaptureAsync(firstSession),
            CaptureAsync(secondSession));

        // Assert
        Assert.Single(attempts, exception => exception is null);
        Assert.Single(attempts, exception => exception is not null);
    }

    private static AgentSession CreateSession(string itemId)
    {
        return new AgentSession
        {
            ItemId = itemId,
            UserId = "user-1",
            UserName = "agent",
            DisplayName = "Agent",
            ConnectionIds = [itemId],
            IsOnline = true,
            CreatedUtc = _now,
            ConnectedUtc = _now,
            LastHeartbeatUtc = _now,
        };
    }

    private static async Task SeedSessionAsync(IStore store)
    {
        await using var session = store.CreateSession();
        await new AgentSessionStore(session).CreateAsync(CreateSession("agent-session-1"), TestContext.Current.CancellationToken);
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
