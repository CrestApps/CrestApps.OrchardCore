using CrestApps.OrchardCore.Asterisk.BackgroundTasks;
using CrestApps.OrchardCore.Asterisk.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskPjsipCredentialCleanupBackgroundTaskTests
{
    [Fact]
    public async Task DoWorkAsync_InvokesCredentialCleanup()
    {
        // Arrange
        var credentialIssuer = new FakeCredentialIssuer();

        await using var serviceProvider = CreateServiceProvider(credentialIssuer);

        // Act
        await new AsteriskPjsipCredentialCleanupBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, credentialIssuer.CleanupCallCount);
    }

    [Fact]
    public async Task DoWorkAsync_WhenCleanupThrows_DoesNotPropagate()
    {
        // Arrange
        var credentialIssuer = new FakeCredentialIssuer { ThrowOnCleanup = true };

        await using var serviceProvider = CreateServiceProvider(credentialIssuer);

        // Act
        await new AsteriskPjsipCredentialCleanupBackgroundTask().DoWorkAsync(serviceProvider, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, credentialIssuer.CleanupCallCount);
    }

    private static ServiceProvider CreateServiceProvider(IAsteriskPjsipCredentialIssuer credentialIssuer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(credentialIssuer);
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    private sealed class FakeCredentialIssuer : IAsteriskPjsipCredentialIssuer
    {
        public int CleanupCallCount { get; private set; }

        public bool ThrowOnCleanup { get; set; }

        public Task<AsteriskPjsipCredential> IssueAsync(
            AsteriskPjsipCredentialIssueRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AsteriskPjsipCredential> RotateAsync(
            AsteriskPjsipCredentialIssueRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RevokeAsync(
            string authorizationUser,
            string reason,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> RevokeUserAsync(
            string userId,
            string reason,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
        {
            CleanupCallCount++;

            if (ThrowOnCleanup)
            {
                throw new InvalidOperationException("cleanup failed");
            }

            return Task.FromResult(CleanupCallCount);
        }
    }
}
