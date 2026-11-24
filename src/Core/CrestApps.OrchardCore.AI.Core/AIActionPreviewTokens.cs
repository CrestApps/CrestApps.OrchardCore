using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// The tokens a preview tool issues and the tool that acts requires, for AI tools that move money or credit (the owner's
/// review, F23), such as <c>previewRefund</c> before <c>refundOrder</c>: the preview shows the user what would happen and
/// returns a random token, and the action runs only with that token. A token is kept in the distributed cache for
/// <see cref="Lifetime"/> under a key made from the purpose, the caller's scope and the token, with the fingerprint of the
/// previewed action. It works once, for the same caller and the same action, and only until it expires; it is taken under
/// a distributed lock, so two actions can never use one token.
/// </summary>
/// <remarks>
/// The purpose (such as <c>orders-refund-preview</c>) is part of the cache key, so the tokens of one tool family are never
/// found by another. The scope names who the token belongs to, such as the tenant and the caller; the fingerprint names
/// the action, such as the record, the amount and the currency, written the same way by the preview and the action.
/// </remarks>
public static class AIActionPreviewTokens
{
    /// <summary>
    /// How long a preview's token can be used.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Issues a token for a previewed action.
    /// </summary>
    /// <param name="cache">The distributed cache the token is kept in.</param>
    /// <param name="purpose">The tool family the token belongs to, such as <c>orders-refund-preview</c>.</param>
    /// <param name="scope">Who the token belongs to, such as the tenant and the caller.</param>
    /// <param name="fingerprint">The fingerprint of the previewed action.</param>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The token and when it expires.</returns>
    public static async Task<(string Token, DateTime ExpiresUtc)> IssueAsync(
        IDistributedCache cache,
        string purpose,
        string scope,
        string fingerprint,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresUtc = utcNow.Add(Lifetime);

        await cache.SetStringAsync(
            GetCacheKey(purpose, scope, token),
            JsonSerializer.Serialize(new StoredPreview(fingerprint, expiresUtc)),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime },
            cancellationToken);

        return (token, expiresUtc);
    }

    /// <summary>
    /// Takes a token for an action: it must have been issued for the same purpose, to the same caller, for the same
    /// action, and not have expired or been taken. A taken token is removed; a token given with another action is left
    /// for the action it was issued for.
    /// </summary>
    /// <param name="cache">The distributed cache the token is kept in.</param>
    /// <param name="locks">The distributed lock that keeps two actions from taking the same token.</param>
    /// <param name="purpose">The tool family the token belongs to, as given to <see cref="IssueAsync"/>.</param>
    /// <param name="scope">Who the token belongs to, as given to <see cref="IssueAsync"/>.</param>
    /// <param name="token">The token.</param>
    /// <param name="fingerprint">The fingerprint of the action.</param>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the token was taken, or why not.</returns>
    public static async Task<AIActionPreviewTokenResult> TakeAsync(
        IDistributedCache cache,
        IDistributedLock locks,
        string purpose,
        string scope,
        string token,
        string fingerprint,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(locks);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        if (string.IsNullOrWhiteSpace(token))
        {
            return AIActionPreviewTokenResult.Unknown;
        }

        var key = GetCacheKey(purpose, scope, token.Trim());
        var (locker, locked) = await locks.TryAcquireLockAsync(key, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return AIActionPreviewTokenResult.Busy;
        }

        await using (locker)
        {
            if (await cache.GetStringAsync(key, cancellationToken) is not { } stored ||
                JsonSerializer.Deserialize<StoredPreview>(stored) is not { } preview)
            {
                return AIActionPreviewTokenResult.Unknown;
            }

            if (preview.ExpiresUtc <= utcNow)
            {
                await cache.RemoveAsync(key, cancellationToken);

                return AIActionPreviewTokenResult.Expired;
            }

            if (!string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return AIActionPreviewTokenResult.Mismatch;
            }

            await cache.RemoveAsync(key, cancellationToken);

            return AIActionPreviewTokenResult.Taken;
        }
    }

    // The token is never kept as it is: the key is the purpose and a hash of the caller's scope and the token, so a token
    // issued to one caller, or for one tool family, is not found for another.
    private static string GetCacheKey(string purpose, string scope, string token)
        => purpose + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{scope}|{token}")));

    private sealed record StoredPreview(string Fingerprint, DateTime ExpiresUtc);
}

/// <summary>
/// What became of a preview's token given to an action.
/// </summary>
public enum AIActionPreviewTokenResult
{
    /// <summary>
    /// The token was valid and is now used up.
    /// </summary>
    Taken,

    /// <summary>
    /// No such token was issued to the caller for this purpose, or it was already used or has expired and was cleared.
    /// </summary>
    Unknown,

    /// <summary>
    /// The token expired.
    /// </summary>
    Expired,

    /// <summary>
    /// The token was issued for another action: another record, amount, currency or options.
    /// </summary>
    Mismatch,

    /// <summary>
    /// Another action is taking the same token at this moment.
    /// </summary>
    Busy,
}
