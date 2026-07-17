using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentLogoutCredentialRevocationTests
{
    [Fact]
    public async Task RevokeCredentialsOnSignOutAsync_WithSingleRevoker_CallsRevokerWithUserIdAndSignedOutReason()
    {
        // Arrange
        var revoker = new FakeSoftPhoneCredentialRevoker("Test");
        var logger = new TestLogger();

        // Act
        await AvailabilityStartup.RevokeCredentialsOnSignOutAsync(
            [revoker],
            "user-1",
            logger,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, revoker.CallCount);
        Assert.Equal("user-1", revoker.LastUserId);
        Assert.Equal("signed-out", revoker.LastReason);
    }

    [Fact]
    public async Task RevokeCredentialsOnSignOutAsync_WhenRevokerThrows_ExceptionDoesNotPropagate()
    {
        // Arrange
        var revoker = new FakeSoftPhoneCredentialRevoker("Test", shouldThrow: true);
        var logger = new TestLogger();

        // Act & Assert — must not throw
        await AvailabilityStartup.RevokeCredentialsOnSignOutAsync(
            [revoker],
            "user-1",
            logger,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RevokeCredentialsOnSignOutAsync_WhenRevokerThrows_LogsWarning()
    {
        // Arrange
        var revoker = new FakeSoftPhoneCredentialRevoker("Test", shouldThrow: true);
        var logger = new TestLogger();

        // Act
        await AvailabilityStartup.RevokeCredentialsOnSignOutAsync(
            [revoker],
            "user-1",
            logger,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, logger.WarningCount);
    }

    [Fact]
    public async Task RevokeCredentialsOnSignOutAsync_WhenFirstRevokerThrows_RemainingRevokersAreStillInvoked()
    {
        // Arrange
        var failing = new FakeSoftPhoneCredentialRevoker("Failing", shouldThrow: true);
        var succeeding = new FakeSoftPhoneCredentialRevoker("Succeeding");
        var logger = new TestLogger();

        // Act
        await AvailabilityStartup.RevokeCredentialsOnSignOutAsync(
            [failing, succeeding],
            "user-1",
            logger,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, succeeding.CallCount);
    }

    [Fact]
    public async Task RevokeCredentialsOnSignOutAsync_WithEmptyRevokerList_CompletesWithoutError()
    {
        // Arrange
        var logger = new TestLogger();

        // Act & Assert — must not throw
        await AvailabilityStartup.RevokeCredentialsOnSignOutAsync(
            [],
            "user-1",
            logger,
            TestContext.Current.CancellationToken);
    }

    private sealed class FakeSoftPhoneCredentialRevoker : ISoftPhoneCredentialRevoker
    {
        private readonly bool _shouldThrow;

        public FakeSoftPhoneCredentialRevoker(
            string providerName,
            bool shouldThrow = false)
        {
            ProviderName = providerName;
            _shouldThrow = shouldThrow;
        }

        public string ProviderName { get; }

        public int CallCount { get; private set; }

        public string LastUserId { get; private set; }

        public string LastReason { get; private set; }

        public Task<int> RevokeForUserAsync(
            string userId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUserId = userId;
            LastReason = reason;

            if (_shouldThrow)
            {
                throw new InvalidOperationException("Simulated revocation failure.");
            }

            return Task.FromResult(1);
        }
    }

    private sealed class TestLogger : ILogger
    {
        public int WarningCount { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                WarningCount++;
            }
        }
    }
}
