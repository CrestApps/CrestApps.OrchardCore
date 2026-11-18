using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Remembers the nonces and token ids it was shown, in the distributed cache (memory, or Redis when Orchard Core's Redis
/// cache is on), so a replayed login request, Deep Linking response or client assertion is refused. A value is
/// remembered at least <see cref="Lifetime"/>, and until the token or data that carries it expires when that is later;
/// the check and the write happen under a distributed lock per value, so two requests with the same value at once let
/// only one through.
/// </summary>
public sealed class LtiNonceStore
{
    /// <summary>
    /// How long a nonce is remembered at least: longer than any token that carries one is valid.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IDistributedCache _cache;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiNonceStore"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="distributedLock">The distributed lock, which makes the check and the write one step.</param>
    /// <param name="clock">The clock.</param>
    public LtiNonceStore(IDistributedCache cache, IDistributedLock distributedLock, IClock clock)
    {
        _cache = cache;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <summary>
    /// Uses a nonce once, remembering it for <see cref="Lifetime"/>.
    /// </summary>
    /// <param name="scope">What the nonce belongs to, such as a client id and a message kind.</param>
    /// <param name="nonce">The nonce.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> the first time; <see langword="false"/> when it was seen before or is empty.</returns>
    public Task<bool> TryUseAsync(string scope, string nonce, CancellationToken cancellationToken = default)
        => UseAsync(scope, nonce, null, cancellationToken);

    /// <summary>
    /// Uses a nonce once, remembering it until what carries it expires (with the clock skew), and at least for
    /// <see cref="Lifetime"/>.
    /// </summary>
    /// <param name="scope">What the nonce belongs to, such as a client id and a message kind.</param>
    /// <param name="nonce">The nonce.</param>
    /// <param name="expiresUtc">When the token or data that carries the nonce expires.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> the first time; <see langword="false"/> when it was seen before, is empty, or
    /// could not be checked.</returns>
    public Task<bool> TryUseAsync(string scope, string nonce, DateTime expiresUtc, CancellationToken cancellationToken = default)
        => UseAsync(scope, nonce, expiresUtc, cancellationToken);

    private async Task<bool> UseAsync(string scope, string nonce, DateTime? expiresUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(nonce) || nonce.Length > 512)
        {
            return false;
        }

        var key = "CrestApps.Lti.Nonce:" + scope + ":" + nonce;
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key + ":Lock", _lockTimeout, _lockExpiration);

        // Not knowing whether the value was used, it is refused.
        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            if (await _cache.GetAsync(key, cancellationToken) is not null)
            {
                return false;
            }

            var remember = Lifetime;

            if (expiresUtc is { } expires && DateTime.SpecifyKind(expires, DateTimeKind.Utc) + LtiTokens.ClockSkew - _clock.UtcNow is { } untilExpiry && untilExpiry > remember)
            {
                remember = untilExpiry;
            }

            await _cache.SetAsync(key, [1], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remember }, cancellationToken);
        }

        return true;
    }
}
