using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ScopedCallSessionUpdaterTests
{
    [Fact]
    public async Task UpdateAsync_WhenAnotherWriterCommitsFirst_AppliesTheChangeToTheirCopy()
    {
        // Arrange: the first commit loses to a webhook that wrote the call in between (live: the restored bridge's
        // call.bridged), so the change must be applied again to what that webhook wrote.
        var store = new CommittedCallSession();
        var executor = new CommitLosingScopeExecutor(store, losses: 1, otherWriter: session => session.AgentId = "agent-from-webhook");
        var updater = new ScopedCallSessionUpdater(executor);
        var seen = new List<CallSession>();

        // Act
        var saved = await updater.UpdateAsync("int-1", session =>
        {
            seen.Add(session);
            session.ProviderCallId = "changed-by-the-request";

            return true;
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(saved);
        Assert.Equal(2, executor.Attempts);
        Assert.Equal(2, seen.Count);
        Assert.NotSame(seen[0], seen[1]);
        Assert.Equal("changed-by-the-request", store.ProviderCallId);
        Assert.Equal("agent-from-webhook", store.AgentId);
    }

    [Fact]
    public async Task UpdateAsync_WhenTheCommitKeepsLosing_GivesUpWithTheConflict()
    {
        // Arrange
        var executor = new CommitLosingScopeExecutor(new CommittedCallSession(), losses: int.MaxValue, otherWriter: _ => { });
        var updater = new ScopedCallSessionUpdater(executor);

        // Act & Assert
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            updater.UpdateAsync("int-1", _ => true, TestContext.Current.CancellationToken));
        Assert.Equal(4, executor.Attempts);
    }

    [Fact]
    public async Task UpdateAsync_WhenNothingChanged_SavesNothing()
    {
        // Arrange
        var store = new CommittedCallSession();
        var executor = new CommitLosingScopeExecutor(store, losses: 0, otherWriter: _ => { });
        var updater = new ScopedCallSessionUpdater(executor);

        // Act
        var saved = await updater.UpdateAsync("int-1", _ => false, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(saved);
        Assert.Equal(0, store.Commits);
    }

    [Fact]
    public async Task UpdateWithInteractionAsync_SavesTheInteractionWithTheCall()
    {
        // Arrange
        var store = new CommittedCallSession();
        var executor = new CommitLosingScopeExecutor(store, losses: 1, otherWriter: _ => { });
        var updater = new ScopedCallSessionUpdater(executor);

        // Act
        var saved = await updater.UpdateWithInteractionAsync("int-1", (session, interaction) =>
        {
            session.AgentId = "supervisor-agent";
            interaction.AgentId = "supervisor-agent";

            return true;
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(saved);
        Assert.Equal("supervisor-agent", store.AgentId);
        Assert.Equal("supervisor-agent", store.InteractionAgentId);
    }

    // The committed state of one call, read as a fresh copy by every scope, as a database would hand it out.
    private sealed class CommittedCallSession
    {
        public string AgentId { get; set; } = "agent-1";

        public string ProviderCallId { get; set; } = "call-1";

        public string InteractionAgentId { get; set; } = "agent-1";

        public int Commits { get; set; }
    }

    // Runs each unit of work in a scope of its own and loses the first commits to another writer.
    private sealed class CommitLosingScopeExecutor : IContactCenterScopeExecutor
    {
        private readonly CommittedCallSession _store;
        private readonly Action<CommittedCallSession> _otherWriter;
        private int _losses;

        public CommitLosingScopeExecutor(CommittedCallSession store, int losses, Action<CallSession> otherWriter)
        {
            _store = store;
            _losses = losses;
            _otherWriter = committed =>
            {
                var session = new CallSession { AgentId = committed.AgentId, ProviderCallId = committed.ProviderCallId };
                otherWriter(session);
                committed.AgentId = session.AgentId;
                committed.ProviderCallId = session.ProviderCallId;
            };
        }

        public int Attempts { get; private set; }

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => throw new NotSupportedException();

        public async Task ExecuteAsync(Func<IServiceProvider, Task> operation)
        {
            Attempts++;

            CallSession pendingSession = null;
            Interaction pendingInteraction = null;

            var sessions = new Mock<ICallSessionManager>();
            sessions
                .Setup(manager => manager.FindByInteractionIdAsync("int-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new CallSession { InteractionId = "int-1", AgentId = _store.AgentId, ProviderCallId = _store.ProviderCallId });
            sessions
                .Setup(manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Callback<CallSession, System.Text.Json.Nodes.JsonNode, CancellationToken>((session, _, _) => pendingSession = session)
                .Returns(ValueTask.CompletedTask);

            var interactions = new Mock<IInteractionManager>();
            interactions
                .Setup(manager => manager.FindByIdAsync("int-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Interaction { ItemId = "int-1", AgentId = _store.InteractionAgentId });
            interactions
                .Setup(manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Callback<Interaction, System.Text.Json.Nodes.JsonNode, CancellationToken>((interaction, _, _) => pendingInteraction = interaction)
                .Returns(ValueTask.CompletedTask);

            await using var services = new ServiceCollection()
                .AddSingleton(sessions.Object)
                .AddSingleton(interactions.Object)
                .BuildServiceProvider();

            await operation(services);

            if (pendingSession is null && pendingInteraction is null)
            {
                return;
            }

            // The commit: lost when another writer got there first, applied otherwise.
            if (_losses > 0)
            {
                _losses--;
                _otherWriter(_store);

                throw new ConcurrencyException(new Document());
            }

            if (pendingSession is not null)
            {
                _store.AgentId = pendingSession.AgentId;
                _store.ProviderCallId = pendingSession.ProviderCallId;
            }

            if (pendingInteraction is not null)
            {
                _store.InteractionAgentId = pendingInteraction.AgentId;
            }

            _store.Commits++;
        }

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => false;

        public bool ScheduleAfterCommit(Func<Task> operation)
            => false;
    }
}
