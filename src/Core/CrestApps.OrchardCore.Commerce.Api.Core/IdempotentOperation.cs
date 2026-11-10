using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Commerce.Api;

/// <summary>
/// Runs an operation once per idempotency key, for a protocol whose key isn't an HTTP header: a message id inside a
/// JSON-RPC body, such as the <c>messageId</c> of an Agent2Agent (A2A) message. A retry with the same key and the same
/// request gets the first answer back; the same key with another request is a conflict; a retry while the first request
/// still runs is told to wait. It keeps <see cref="IdempotencyEndpointFilter"/>'s rules: answers are kept per scope and
/// key, under a distributed lock, and only final answers are kept. Endpoints whose key is a header use the filter.
/// </summary>
public static class IdempotentOperation
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Runs <paramref name="operation"/> unless an answer is already kept for the key.
    /// </summary>
    /// <param name="cache">The distributed cache the answers are kept in.</param>
    /// <param name="locks">The distributed lock that keeps two requests with the same key from running at once.</param>
    /// <param name="scope">Who and what the key belongs to, such as the tenant, the caller and the operation.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="fingerprint">The request's fingerprint, which a retry with the same key must match.</param>
    /// <param name="lifetime">How long an answer is kept.</param>
    /// <param name="operation">The operation; its answer says whether it is final and kept.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<IdempotentOperationResult> RunAsync(
        IDistributedCache cache,
        IDistributedLock locks,
        string scope,
        string key,
        string fingerprint,
        TimeSpan lifetime,
        Func<CancellationToken, Task<IdempotentOperationAnswer>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(locks);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(operation);

        var cacheKey = GetCacheKey(scope, key);

        if (await cache.GetStringAsync(cacheKey, cancellationToken) is { } stored)
        {
            return Replay(stored, fingerprint);
        }

        var (locker, locked) = await locks.TryAcquireLockAsync(cacheKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new IdempotentOperationResult(IdempotentOperationOutcome.InFlight, null);
        }

        await using (locker)
        {
            if (await cache.GetStringAsync(cacheKey, cancellationToken) is { } storedMeanwhile)
            {
                return Replay(storedMeanwhile, fingerprint);
            }

            var answer = await operation(cancellationToken);

            if (answer.Keep)
            {
                await cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(new StoredAnswer(fingerprint, answer.Value)),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime },
                    cancellationToken);
            }

            return new IdempotentOperationResult(IdempotentOperationOutcome.Ran, answer.Value);
        }
    }

    internal static string GetCacheKey(string scope, string key)
        => "commerce-idempotent-operation:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{scope}|{key}")));

    private static IdempotentOperationResult Replay(string stored, string fingerprint)
    {
        var answer = JsonSerializer.Deserialize<StoredAnswer>(stored);

        // The same key with another request is a mistake the caller must fix, not a retry.
        return string.Equals(answer.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new IdempotentOperationResult(IdempotentOperationOutcome.Replayed, answer.Value)
            : new IdempotentOperationResult(IdempotentOperationOutcome.Conflict, null);
    }

    private sealed record StoredAnswer(string Fingerprint, string Value);
}

/// <summary>
/// What an idempotent operation answered.
/// </summary>
/// <param name="Value">The answer, as text, kept for a retry.</param>
/// <param name="Keep">Whether the answer is final and kept; a busy, rate-limited or failed answer is tried afresh.</param>
public sealed record IdempotentOperationAnswer(string Value, bool Keep);

/// <summary>
/// The outcome of <see cref="IdempotentOperation.RunAsync"/>.
/// </summary>
/// <param name="Outcome">Whether the operation ran, was replayed, conflicts or is still running.</param>
/// <param name="Value">The answer when it ran or was replayed; otherwise <see langword="null"/>.</param>
public sealed record IdempotentOperationResult(IdempotentOperationOutcome Outcome, string Value);

/// <summary>
/// The outcomes of an idempotent operation.
/// </summary>
public enum IdempotentOperationOutcome
{
    /// <summary>
    /// The operation ran.
    /// </summary>
    Ran,

    /// <summary>
    /// The first answer for the key was returned without running the operation again.
    /// </summary>
    Replayed,

    /// <summary>
    /// The key was used for another request.
    /// </summary>
    Conflict,

    /// <summary>
    /// The first request with the key is still running.
    /// </summary>
    InFlight,
}
