using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Provides the default <see cref="IVoiceIngressGate"/> implementation on top of the tenant-scoped
/// distributed lock.
/// </summary>
public sealed class VoiceIngressGate : IVoiceIngressGate
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    // The held-key set flows with the asynchronous control flow rather than with the dependency-injection
    // scope, because a projection is allowed to re-enter ingestion from a fresh shell scope while the outer
    // lease is still held. A scope-local set would not see the outer lease and would contend with it until
    // the acquisition timed out.
    private static readonly AsyncLocal<HashSet<string>> _heldKeys = new();

    private readonly IDistributedLock _distributedLock;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceIngressGate"/> class.
    /// </summary>
    /// <param name="distributedLock">The tenant-scoped distributed lock used to serialize each provider call stream.</param>
    public VoiceIngressGate(IDistributedLock distributedLock)
    {
        _distributedLock = distributedLock;
    }

    /// <inheritdoc/>
    public Task<IAsyncDisposable> AcquireAsync(
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerCallId);

        cancellationToken.ThrowIfCancellationRequested();

        var key = VoiceIngressKeys.BuildIngestionLockKey(providerName, providerCallId);
        var held = _heldKeys.Value;

        if (held is null)
        {
            held = new HashSet<string>(StringComparer.Ordinal);

            // The registration happens in this synchronous frame on purpose. Assigning an asynchronous-local
            // value from inside an `async` method confines it to that method's execution context and reverts
            // it the moment the method returns, so a nested consumer would never observe the lease this call
            // is about to take and would contend with it until the acquisition timed out.
            _heldKeys.Value = held;
        }

        if (!held.Add(key))
        {
            return Task.FromResult<IAsyncDisposable>(ReentrantVoiceIngressLease.Instance);
        }

        return AcquireCoreAsync(key, held);
    }

    private async Task<IAsyncDisposable> AcquireCoreAsync(string key, HashSet<string> heldKeys)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(key, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            heldKeys.Remove(key);

            throw new TimeoutException("The provider call event could not acquire its ingestion lock.");
        }

        return new VoiceIngressLease(key, locker);
    }

    /// <inheritdoc/>
    public bool IsHeld(string providerName, string providerCallId)
    {
        if (string.IsNullOrEmpty(providerCallId))
        {
            return false;
        }

        return _heldKeys.Value?.Contains(VoiceIngressKeys.BuildIngestionLockKey(providerName, providerCallId)) == true;
    }

    private sealed class VoiceIngressLease : IAsyncDisposable
    {
        private readonly string _key;
        private readonly ILocker _locker;
        private bool _disposed;

        public VoiceIngressLease(string key, ILocker locker)
        {
            _key = key;
            _locker = locker;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _heldKeys.Value?.Remove(_key);

            if (_locker is not null)
            {
                await _locker.DisposeAsync();
            }
        }
    }

    private sealed class ReentrantVoiceIngressLease : IAsyncDisposable
    {
        public static readonly ReentrantVoiceIngressLease Instance = new();

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
