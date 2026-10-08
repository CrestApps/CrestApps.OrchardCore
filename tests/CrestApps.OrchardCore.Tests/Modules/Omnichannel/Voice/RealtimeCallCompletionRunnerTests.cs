using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A live call is finished on a scope of its own once its session ends, and that scope must not run on the
/// thread of the request the session held.
/// </summary>
public sealed class RealtimeCallCompletionRunnerTests
{
    [Fact]
    public async Task FinishingACall_DoesNotRunOnTheCallersThread()
    {
        // Arrange
        // On SQLite every YesSql call completes synchronously, so an un-awaited scope ran inline until something
        // truly asynchronous yielded -- a commit included. Made while the request's own session still held the
        // write lock, that commit blocked the request's thread for the busy timeout. The blocking loop stands in
        // for that commit.
        using var released = new ManualResetEventSlim();
        var logger = new ErrorLogger();
        var runner = new RealtimeCallCompletionRunner(
            CreateShellHost(services => services.AddScoped<VoiceAgentConversationLoop>(_ =>
            {
                released.Wait(TimeSpan.FromSeconds(30));

                throw new InvalidOperationException("SQLite Error 5: 'database is locked'.");
            })),
            new ShellSettings { Name = "Default" },
            logger);

        // Act
        // Run off the test thread so the inline behavior fails this test instead of hanging it.
        var running = Task.Run(() => runner.RunAsync(new RealtimeCallCompletion { ActivityId = "activity-1", EndCallRequested = true }), TestContext.Current.CancellationToken);
        var returnedWhileFinishing = await Task.WhenAny(running, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)) == running;
        released.Set();

        // Assert
        Assert.True(returnedWhileFinishing);

        // And what went wrong in that scope is still reported, though nobody awaits it.
        var error = await logger.Failed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Contains("activity-1", error);
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

    private sealed class ErrorLogger : ILogger<RealtimeCallCompletionRunner>
    {
        public TaskCompletionSource<string> Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Failed.TrySetResult(formatter(state, exception));
            }
        }
    }
}
