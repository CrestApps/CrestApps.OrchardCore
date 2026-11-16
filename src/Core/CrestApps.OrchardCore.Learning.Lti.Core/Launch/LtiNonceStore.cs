using Microsoft.Extensions.Caching.Distributed;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Remembers the nonces and token ids it was shown, in the distributed cache (memory, or Redis when Orchard Core's Redis
/// cache is on), so a replayed login request, Deep Linking response or client assertion is refused.
/// </summary>
public sealed class LtiNonceStore
{
    /// <summary>
    /// How long a nonce is remembered: longer than any token that carries one is valid.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiNonceStore"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    public LtiNonceStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Uses a nonce once.
    /// </summary>
    /// <param name="scope">What the nonce belongs to, such as a client id and a message kind.</param>
    /// <param name="nonce">The nonce.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> the first time; <see langword="false"/> when it was seen before or is empty.</returns>
    public async Task<bool> TryUseAsync(string scope, string nonce, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(nonce) || nonce.Length > 512)
        {
            return false;
        }

        var key = "CrestApps.Lti.Nonce:" + scope + ":" + nonce;

        if (await _cache.GetAsync(key, cancellationToken) is not null)
        {
            return false;
        }

        await _cache.SetAsync(key, [1], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime }, cancellationToken);

        return true;
    }
}
