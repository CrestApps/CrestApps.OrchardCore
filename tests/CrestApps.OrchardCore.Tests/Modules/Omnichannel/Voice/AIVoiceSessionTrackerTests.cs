using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A turn-based call is a string of provider events with nothing held between them, so what is measured across
/// them is kept here until the call ends.
/// </summary>
public sealed class AIVoiceSessionTrackerTests
{
    private static readonly DateTime _answered = new(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ATurnBasedCall_IsMeasuredFromTheProviderSpeechEvents()
    {
        // Arrange
        var tracker = CreateTracker();
        tracker.BeginTurnBased("activity-1", _answered);

        // Act
        tracker.TurnBasedSpeech("activity-1", started: true, _answered.AddSeconds(1));
        tracker.TurnBasedSpeech("activity-1", started: false, _answered.AddSeconds(4));
        tracker.TurnBasedSpeech("activity-1", started: true, _answered.AddSeconds(10));
        tracker.TurnBasedSpeech("activity-1", started: false, _answered.AddSeconds(12));
        var measured = tracker.EndTurnBased("activity-1", _answered.AddSeconds(15));

        // Assert
        Assert.Equal(15_000, measured.SessionDurationMs);
        Assert.Equal(5_000, measured.AssistantSpeakingMs);
        Assert.Equal(1_000, measured.TimeToFirstAssistantAudioMs);
        Assert.Null(measured.CallerSpeakingMs);
    }

    [Fact]
    public void ACallThisNodeNeverSawAnswered_HasNoMeasurement()
    {
        // Arrange
        var tracker = CreateTracker();

        // Act
        tracker.TurnBasedSpeech("activity-1", started: true, _answered);

        // Assert
        Assert.Null(tracker.EndTurnBased("activity-1", _answered.AddSeconds(5)));
    }

    [Fact]
    public void ACallIsEndedOnce()
    {
        // Arrange
        // A handed-off call ends at the handoff, and the provider's hangup for it arrives minutes later.
        var tracker = CreateTracker();
        tracker.BeginTurnBased("activity-1", _answered);

        // Act
        var first = tracker.EndTurnBased("activity-1", _answered.AddSeconds(5));
        var second = tracker.EndTurnBased("activity-1", _answered.AddSeconds(500));

        // Assert
        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void CallsAreKeptApart()
    {
        // Arrange
        var tracker = CreateTracker();
        tracker.BeginTurnBased("activity-1", _answered);
        tracker.BeginTurnBased("activity-2", _answered);

        // Act
        tracker.TurnBasedSpeech("activity-2", started: true, _answered.AddSeconds(1));
        tracker.TurnBasedSpeech("activity-2", started: false, _answered.AddSeconds(3));

        // Assert
        Assert.Equal(0, tracker.EndTurnBased("activity-1", _answered.AddSeconds(5)).AssistantSpeakingMs);
        Assert.Equal(2_000, tracker.EndTurnBased("activity-2", _answered.AddSeconds(5)).AssistantSpeakingMs);
    }

    [Fact]
    public async Task RecordingASummary_DoesNotWriteItOnTheCallersThread()
    {
        // Arrange
        // On SQLite every YesSql call completes synchronously, and a write that meets a held write lock blocks its
        // thread for the 30 second busy timeout. Started inline, the summary's scope ran on the request's thread
        // while that request still held the write lock: it waited the full timeout on its own request, stalling
        // the tenant, and then failed where nothing saw it. The blocking writer stands in for that commit.
        using var released = new ManualResetEventSlim();
        var logger = new WarningLogger<AIVoiceSessionTracker>();
        var tracker = new AIVoiceSessionTracker(
            CreateShellHost(services => services.AddScoped<AIVoiceSessionSummaryWriter>(_ =>
            {
                released.Wait(TimeSpan.FromSeconds(30));

                throw new InvalidOperationException("SQLite Error 5: 'database is locked'.");
            })),
            new ShellSettings { Name = "Default" },
            logger);

        // Act
        // Run off the test thread so the inline behavior fails this test instead of hanging it.
        var recording = Task.Run(() => tracker.RecordAsync(new AIVoiceSessionDraft { ActivityId = "activity-1" }), TestContext.Current.CancellationToken);
        var returnedWhileWriting = await Task.WhenAny(recording, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)) == recording;
        released.Set();

        // Assert
        Assert.True(returnedWhileWriting);
    }

    [Fact]
    public async Task ASummaryTheDatabaseRefuses_IsReported_RatherThanLostUnseen()
    {
        // Arrange
        // The refusal came from the scope's commit, after the writer had returned, and the scope's task was
        // discarded, so the summary vanished without a line in the log. The commit is the scope's last step, so a
        // failing one is registered the same way the document store registers its own.
        var logger = new WarningLogger<AIVoiceSessionTracker>();
        var tracker = new AIVoiceSessionTracker(
            CreateShellHost(services => services.AddScoped(_ =>
            {
                ShellScope.RegisterBeforeDispose(_ => Task.FromException(new InvalidOperationException("SQLite Error 5: 'database is locked'.")));

                return CreateWriterThatWritesNothing();
            })),
            new ShellSettings { Name = "Default" },
            logger);

        // Act
        await tracker.RecordAsync(new AIVoiceSessionDraft { ActivityId = "activity-1" });

        // Assert
        var warning = await logger.Warned.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Contains("activity-1", warning);
    }

    private static AIVoiceSessionTracker CreateTracker()
        => new(shellHost: null, shellSettings: null, NullLogger<AIVoiceSessionTracker>.Instance);

    private static AIVoiceSessionSummaryWriter CreateWriterThatWritesNothing()
    {
        // A tenant that does not track usage: the writer returns before it touches anything else.
        var options = new Mock<IOptionsMonitor<GeneralAIOptions>>();
        options.SetupGet(monitor => monitor.CurrentValue).Returns(new GeneralAIOptions { EnableAIUsageTracking = false });

        return new AIVoiceSessionSummaryWriter(null, null, null, null, null, null, options.Object, null, NullLogger<AIVoiceSessionSummaryWriter>.Instance);
    }

    private static IShellHost CreateShellHost(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);

        var shellContext = new ShellContext
        {
            Settings = new ShellSettings { Name = "Default" },
            ServiceProvider = services.BuildServiceProvider(),
            IsActivated = true,
        };

        var shellHost = new Mock<IShellHost>();
        shellHost
            .Setup(host => host.GetScopeAsync(It.IsAny<ShellSettings>()))
            .ReturnsAsync(() => new ShellScope(shellContext));

        return shellHost.Object;
    }

    private sealed class WarningLogger<T> : ILogger<T>
    {
        public TaskCompletionSource<string> Warned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warned.TrySetResult(formatter(state, exception));
            }
        }
    }
}
