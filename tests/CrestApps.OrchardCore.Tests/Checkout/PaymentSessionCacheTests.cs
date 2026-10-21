using CrestApps.OrchardCore.Checkout.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The payment session cache waits for its entry's lock once, with a timeout and an expiration, rather than polling
/// for it.
/// </summary>
public sealed class PaymentSessionCacheTests
{
    private readonly RecordingDistributedCache _distributedCache = new();

    [Fact]
    public async Task SetAsync_AcquiresTheLockOnce_WithATimeoutAndAnExpiration()
    {
        // Arrange
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), true));

        var cache = CreateCache(distributedLock.Object);

        // Act
        await cache.SetAsync("session-1", "purpose", "value");

        // Assert
        distributedLock.Verify(
            value => value.TryAcquireLockAsync(
                It.Is<string>(key => key.Contains("session-1", StringComparison.Ordinal)),
                It.Is<TimeSpan>(timeout => timeout > TimeSpan.Zero),
                It.Is<TimeSpan?>(expiration => expiration.HasValue && expiration.Value > TimeSpan.Zero)),
            Times.Once);
        Assert.Equal("value", await CreateCache(new LocalLock(NullLogger<LocalLock>.Instance), _distributedCache).GetAsync<string>("session-1", "purpose"));
    }

    [Fact]
    public async Task SetAsync_WhenTheLockCannotBeHad_ThrowsWithoutTryingAgainOrWriting()
    {
        // Arrange
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, false));

        var cache = CreateCache(distributedLock.Object);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SetAsync("session-1", "purpose", "value"));

        // Assert
        distributedLock.Verify(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()), Times.Once);
        Assert.Empty(_distributedCache.Keys);
    }

    private PaymentSessionCache CreateCache(IDistributedLock distributedLock)
        => CreateCache(distributedLock, _distributedCache);

    private static PaymentSessionCache CreateCache(IDistributedLock distributedLock, IDistributedCache distributedCache)
        => new(
            distributedCache,
            Options.Create(new PaymentSessionCacheOptions { MaxLiveSession = TimeSpan.FromMinutes(30) }),
            distributedLock,
            new ShellSettings { Name = "Default" });

    private sealed class RecordingDistributedCache : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public HashSet<string> Keys { get; } = [];

        public byte[] Get(string key)
            => _inner.Get(key);

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            => _inner.GetAsync(key, token);

        public void Refresh(string key)
            => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default)
            => _inner.RefreshAsync(key, token);

        public void Remove(string key)
        {
            Keys.Remove(key);
            _inner.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Keys.Remove(key);

            return _inner.RemoveAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Keys.Add(key);
            _inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Keys.Add(key);

            return _inner.SetAsync(key, value, options, token);
        }
    }
}
