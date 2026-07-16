using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelephonyCommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationCompletes_UsesServerOwnedToken()
    {
        // Arrange
        using var connectionCancellation = new CancellationTokenSource();
        var executor = CreateExecutor();
        CancellationToken providerToken = default;

        // Act
        var operation = executor.ExecuteAsync(async cancellationToken =>
        {
            providerToken = cancellationToken;
            connectionCancellation.Cancel();
            await Task.Yield();

            return "completed";
        });
        var result = await operation;

        // Assert
        Assert.Equal("completed", result);
        Assert.True(connectionCancellation.IsCancellationRequested);
        Assert.False(providerToken.IsCancellationRequested);
        Assert.NotEqual(connectionCancellation.Token, providerToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServerDeadlineExpires_ThrowsTimeout()
    {
        // Arrange
        var executor = CreateExecutor(TimeSpan.FromSeconds(1));

        // Act
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            executor.ExecuteAsync(async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

                return true;
            }));

        // Assert
        Assert.Contains("telephony command", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationIgnoresCancellation_ReturnsAtServerDeadline()
    {
        // Arrange
        var executor = CreateExecutor(TimeSpan.FromSeconds(1));
        var releaseOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            executor.ExecuteAsync(async _ =>
            {
                await releaseOperation.Task;
                operationCompleted.SetResult();

                return true;
            })).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        releaseOperation.SetResult();
        await operationCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("telephony command", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationSwallowsDeadlineCancellation_StillThrowsTimeout()
    {
        // Arrange
        var executor = CreateExecutor(TimeSpan.FromSeconds(1));

        // Act
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            executor.ExecuteAsync(async cancellationToken =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                return true;
            }));

        // Assert
        Assert.Contains("telephony command", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApplicationStops_ThrowsCancellationInsteadOfTimeout()
    {
        // Arrange
        using var applicationStopping = new CancellationTokenSource();
        var hostApplicationLifetime = new Mock<IHostApplicationLifetime>();
        hostApplicationLifetime
            .SetupGet(lifetime => lifetime.ApplicationStopping)
            .Returns(applicationStopping.Token);
        var executor = CreateExecutor(TimeSpan.FromSeconds(10), hostApplicationLifetime.Object);
        var operationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = executor.ExecuteAsync(async cancellationToken =>
        {
            operationStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return true;
        });
        await operationStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        applicationStopping.Cancel();
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => operation);

        // Assert
        Assert.Contains("application is stopping", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Constructor_WhenTimeoutIsOutsideSupportedRange_ThrowsValidationException(int timeoutSeconds)
    {
        // Arrange
        var options = Options.Create(new TelephonyCommandOptions
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        });

        // Act
        var exception = Assert.Throws<OptionsValidationException>(() =>
            new DefaultTelephonyCommandExecutor(options, Mock.Of<IHostApplicationLifetime>()));

        // Assert
        Assert.Contains("between one second and two minutes", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TelephonyHub_MutationsUseServerOwnedTokenAndPersistenceUsesNonExpiringToken()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.Telephony",
            "Hubs",
            "TelephonyHub.cs"));
        var executeStart = source.IndexOf(
            "private async Task<TelephonyResult> ExecuteAsync(",
            StringComparison.Ordinal);
        var executeEnd = source.IndexOf(
            "private void LogHubActionStart(",
            executeStart,
            StringComparison.Ordinal);
        var recordStart = source.IndexOf(
            "private async Task RecordInteractionAsync(",
            StringComparison.Ordinal);
        var recordEnd = source.IndexOf(
            "private async Task<bool> AuthorizeAsync(",
            recordStart,
            StringComparison.Ordinal);

        Assert.True(executeStart >= 0, "ExecuteAsync was not found in TelephonyHub.cs.");
        Assert.True(executeEnd >= 0, "LogHubActionStart was not found after ExecuteAsync in TelephonyHub.cs.");
        Assert.True(recordStart >= 0, "RecordInteractionAsync was not found in TelephonyHub.cs.");
        Assert.True(recordEnd >= 0, "AuthorizeAsync was not found after RecordInteractionAsync in TelephonyHub.cs.");

        // Act
        var mutationSource = source.Substring(executeStart, executeEnd - executeStart);
        var persistenceSource = source.Substring(recordStart, recordEnd - recordStart);

        // Assert
        Assert.Contains("ITelephonyCommandExecutor", mutationSource, StringComparison.Ordinal);
        Assert.Contains("commandCancellationToken", mutationSource, StringComparison.Ordinal);
        Assert.Contains("CancellationToken.None", mutationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Context.ConnectionAborted", mutationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Context.ConnectionAborted", persistenceSource, StringComparison.Ordinal);
    }

    private static DefaultTelephonyCommandExecutor CreateExecutor(
        TimeSpan? timeout = null,
        IHostApplicationLifetime hostApplicationLifetime = null)
    {
        return new DefaultTelephonyCommandExecutor(Options.Create(new TelephonyCommandOptions
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(10),
        }), hostApplicationLifetime ?? Mock.Of<IHostApplicationLifetime>());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Unable to locate the repository root.");
    }
}
