using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Remembers the agent request signatures the store verified, for as long as they could be valid, so a captured
/// request can't be sent again. A signature is remembered by who signed it and the bytes it was verified over, not by
/// its text, so another spelling of the same signature (padding, a DER encoding, another label) is the same signature.
/// Only a signature bound to a time is remembered: one without a time would be valid again once forgotten, so it is
/// refused before it gets here or, for legacy ACP signatures, not guarded at all. The same signature is accepted again
/// only for the same idempotent request (the same <c>Idempotency-Key</c>), which the idempotency layer answers with its
/// first response; callers pass the key only for an endpoint that has that layer. Each check takes a lock on the
/// signature, so parallel copies can't all pass; a lock that can't be taken is <see cref="AgentSignatureUse.Unavailable"/>,
/// not a replay. With a shared cache and lock (Redis), every node knows what another accepted, and otherwise each node
/// keeps its own. A signature is remembered a little longer than it could be valid, so a copy that arrives as the window
/// closes, or on a node whose clock is slightly behind, is still known.
/// </summary>
public sealed class AgentSignatureReplayGuard
{
    private const string KeyPrefix = "agentic-commerce:signature:";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _margin = TimeSpan.FromSeconds(30);

    private readonly IDistributedCache _cache;
    private readonly IDistributedLock _distributedLock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSignatureReplayGuard"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    public AgentSignatureReplayGuard(IDistributedCache cache, IDistributedLock distributedLock)
    {
        _cache = cache;
        _distributedLock = distributedLock;
    }

    /// <summary>
    /// Returns whether a verified signature may be used now, and remembers it for <paramref name="window"/>.
    /// </summary>
    /// <param name="signer">Who signed: the platform and, when it has several, the key.</param>
    /// <param name="signedInput">The bytes the signature was verified over: the signature base or the signing input.</param>
    /// <param name="idempotencyKey">The request's idempotency key, or <see langword="null"/>.</param>
    /// <param name="window">How long the signature could be valid for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="AgentSignatureUse.Replayed"/> when it was used before, by a request that isn't the same idempotent one;
    /// <see cref="AgentSignatureUse.Unavailable"/> when its lock couldn't be taken.
    /// </returns>
    public async Task<AgentSignatureUse> UseAsync(string signer, byte[] signedInput, string idempotencyKey, TimeSpan window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signedInput);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        hash.AppendData(Encoding.UTF8.GetBytes(signer ?? string.Empty));
        hash.AppendData([0]);
        hash.AppendData(signedInput);

        var key = KeyPrefix + Convert.ToHexStringLower(hash.GetHashAndReset());
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return AgentSignatureUse.Unavailable;
        }

        await using (locker)
        {
            var seen = await _cache.GetStringAsync(key, cancellationToken);

            if (seen is not null)
            {
                return !string.IsNullOrEmpty(idempotencyKey) && string.Equals(seen, idempotencyKey, StringComparison.Ordinal)
                    ? AgentSignatureUse.Accepted
                    : AgentSignatureUse.Replayed;
            }

            await _cache.SetStringAsync(key, idempotencyKey ?? string.Empty, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = (window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(10)) + _margin,
            }, cancellationToken);

            return AgentSignatureUse.Accepted;
        }
    }
}
