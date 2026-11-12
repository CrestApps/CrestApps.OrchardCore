using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Remembers the agent request signatures the store accepted, for as long as they could be valid, so a captured
/// request can't be sent again. The same signature is accepted again only for the same idempotent request (the same
/// <c>Idempotency-Key</c>), which the idempotency layer answers with its first response; the cache is the distributed
/// one, so every node knows what another accepted.
/// </summary>
public sealed class AgentSignatureReplayGuard
{
    private const string KeyPrefix = "agentic-commerce:signature:";

    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSignatureReplayGuard"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    public AgentSignatureReplayGuard(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Returns whether a signature may be used now, and remembers it for <paramref name="window"/>.
    /// </summary>
    /// <param name="signature">The signature, as sent.</param>
    /// <param name="idempotencyKey">The request's idempotency key, or <see langword="null"/>.</param>
    /// <param name="window">How long the signature could be valid for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="false"/> when it was used before, by a request that isn't the same idempotent one.</returns>
    public async Task<bool> TryUseAsync(string signature, string idempotencyKey, TimeSpan window, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return true;
        }

        var key = KeyPrefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signature.Trim())));
        var seen = await _cache.GetStringAsync(key, cancellationToken);

        if (seen is not null)
        {
            return !string.IsNullOrEmpty(idempotencyKey) && string.Equals(seen, idempotencyKey, StringComparison.Ordinal);
        }

        await _cache.SetStringAsync(key, idempotencyKey ?? string.Empty, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(10),
        }, cancellationToken);

        return true;
    }
}
