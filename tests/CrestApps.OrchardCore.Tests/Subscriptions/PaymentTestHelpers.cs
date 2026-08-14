using CrestApps.OrchardCore.Subscriptions.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Shared builders for exercising the subscription payment pipeline against real, in-memory
/// infrastructure rather than mocks, so the caching/serialization behavior is covered as well.
/// </summary>
internal static class PaymentTestHelpers
{
    /// <summary>
    /// Creates a <see cref="SubscriptionPaymentSession"/> backed by an in-memory distributed cache and a
    /// lock that is always granted, which is representative of the single-instance (local lock) deployment.
    /// </summary>
    public static SubscriptionPaymentSession CreatePaymentSession(string tenantName = "Default")
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        var options = Options.Create(new SubscriptionPaymentSessionOptions
        {
            MaxLiveSession = TimeSpan.FromMinutes(30),
        });

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var shellSettings = new ShellSettings
        {
            Name = tenantName,
        };

        return new SubscriptionPaymentSession(cache, options, distributedLock.Object, shellSettings);
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
