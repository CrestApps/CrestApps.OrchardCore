using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Core.Services;
using CrestApps.OrchardCore.Wizard.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Wizard;

public sealed class DefaultWizardEngineTests
{
    [Fact]
    public async Task CompleteAsync_WhenAllStepsSaved_CompletesAndFiresCompletedHandler()
    {
        // Arrange
        var session = CreateSession();
        session.Steps.Add(new WizardStep { Key = "step1", CollectData = true });
        session.SavedSteps["step1"] = new System.Text.Json.Nodes.JsonObject();

        var handler = new RecordingWizardHandler();
        var engine = CreateEngine(session, handler);

        // Act
        var result = await engine.CompleteAsync(new WizardFlow(session), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WizardCompletionStatus.Completed, result.Status);
        Assert.Equal(WizardSessionStatus.Completed, session.Status);
        Assert.True(handler.CompletedCalled);
        Assert.False(handler.FailedCalled);
    }

    [Fact]
    public async Task CompleteAsync_WhenStepIncomplete_ReturnsBlocked()
    {
        // Arrange
        var session = CreateSession();
        session.Steps.Add(new WizardStep { Key = "step1", CollectData = true });

        var handler = new RecordingWizardHandler();
        var engine = CreateEngine(session, handler);

        // Act
        var result = await engine.CompleteAsync(new WizardFlow(session), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WizardCompletionStatus.Blocked, result.Status);
        Assert.Equal("step1", result.BlockingStepKey);
        Assert.NotEqual(WizardSessionStatus.Completed, session.Status);
    }

    [Fact]
    public async Task CompleteAsync_WhenAlreadyCompleted_ReturnsAlreadyCompleted()
    {
        // Arrange
        var session = CreateSession();
        session.Status = WizardSessionStatus.Completed;

        var handler = new RecordingWizardHandler();
        var engine = CreateEngine(session, handler);

        // Act
        var result = await engine.CompleteAsync(new WizardFlow(session), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WizardCompletionStatus.AlreadyCompleted, result.Status);
        Assert.False(handler.CompletedCalled);
    }

    [Fact]
    public async Task CompleteAsync_WhenPrepareUnderLockRejects_FailsAndFiresFailedHandler()
    {
        // Arrange
        var session = CreateSession();

        var handler = new RecordingWizardHandler();
        var engine = CreateEngine(session, handler);

        // Act
        var result = await engine.CompleteAsync(new WizardFlow(session), _ => Task.FromResult(false), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WizardCompletionStatus.Failed, result.Status);
        Assert.Equal(WizardSessionStatus.Failed, session.Status);
        Assert.True(handler.FailedCalled);
        Assert.False(handler.CompletedCalled);
    }

    [Fact]
    public async Task CompleteAsync_WhenCompletingHandlerThrows_FailsAndFiresFailedHandler()
    {
        // Arrange
        var session = CreateSession();

        var handler = new RecordingWizardHandler
        {
            ThrowOnCompleting = true,
        };

        var engine = CreateEngine(session, handler);

        // Act
        var result = await engine.CompleteAsync(new WizardFlow(session), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WizardCompletionStatus.Failed, result.Status);
        Assert.Equal(WizardSessionStatus.Failed, session.Status);
        Assert.True(handler.FailedCalled);
        Assert.False(handler.CompletedCalled);
    }

    private static WizardSession CreateSession()
        => new()
        {
            SessionId = "session-1",
            WizardType = "Test",
            Status = WizardSessionStatus.Pending,
        };

    private static DefaultWizardEngine CreateEngine(WizardSession session, IWizardHandler handler)
    {
        var store = new InMemoryWizardSessionStore(session);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        var yesSqlSession = new Mock<ISession>();

        return new DefaultWizardEngine(
            store,
            [handler],
            distributedLock.Object,
            yesSqlSession.Object,
            clock.Object,
            NullLogger<DefaultWizardEngine>.Instance);
    }

    private sealed class InMemoryWizardSessionStore : IWizardSessionStore
    {
        private readonly WizardSession _session;

        public InMemoryWizardSessionStore(WizardSession session)
        {
            _session = session;
        }

        public Task<WizardSession> GetAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(sessionId, _session.SessionId, StringComparison.Ordinal) ? _session : null);

        public Task<WizardSession> GetAsync(string sessionId, WizardSessionStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(sessionId, _session.SessionId, StringComparison.Ordinal) && _session.Status == status ? _session : null);

        public Task<WizardSession> NewAsync(string wizardType, string definitionId = null, string definitionVersionId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_session);

        public Task SaveAsync(WizardSession session, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingWizardHandler : WizardHandlerBase
    {
        public bool CompletedCalled { get; private set; }

        public bool FailedCalled { get; private set; }

        public bool ThrowOnCompleting { get; set; }

        public override Task CompletingAsync(WizardFlowCompletingContext context)
        {
            if (ThrowOnCompleting)
            {
                throw new InvalidOperationException("Completing failed.");
            }

            return Task.CompletedTask;
        }

        public override Task CompletedAsync(WizardFlowCompletedContext context)
        {
            CompletedCalled = true;

            return Task.CompletedTask;
        }

        public override Task FailedAsync(WizardFlowFailedContext context)
        {
            FailedCalled = true;

            return Task.CompletedTask;
        }
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
