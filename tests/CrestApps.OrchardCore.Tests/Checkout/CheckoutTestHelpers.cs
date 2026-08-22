using CrestApps.OrchardCore.Checkout.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Shared builders for exercising the checkout payment pipeline against real, in-memory infrastructure
/// rather than mocks, so caching and serialization behavior is covered as well.
/// </summary>
internal static class CheckoutTestHelpers
{
    /// <summary>
    /// Creates a <see cref="PaymentSessionCache"/> backed by an in-memory distributed cache and a lock
    /// that is always granted, which is representative of a single-instance (local lock) deployment.
    /// </summary>
    public static PaymentSessionCache CreatePaymentSessionCache(string tenantName = "Default")
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        var options = Options.Create(new PaymentSessionCacheOptions
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

        return new PaymentSessionCache(cache, options, distributedLock.Object, shellSettings);
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
