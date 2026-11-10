using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// ACP's idempotency rules for the MCP binding, where the key comes in <c>meta.idempotency_key</c> instead of a header
/// and the answer is a tool result instead of an HTTP response. The rules are the REST binding's
/// (<see cref="AcpEndpoints.Idempotency"/>): a key on every mutating call, 1 to 255 characters, kept a day per platform,
/// tool and checkout; the same key with an equal payload (RFC 8785) replays the first answer, another payload gets 422
/// <c>idempotency_conflict</c>, and a call while the first still runs gets 409 <c>idempotency_in_flight</c>. Server
/// errors, "too many requests" and conflicts that clear by themselves aren't kept.
/// </summary>
public sealed class AcpMcpIdempotency
{
    private readonly IDistributedCache _cache;
    private readonly IDistributedLock _distributedLock;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcpMcpIdempotency"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache answers are kept in.</param>
    /// <param name="distributedLock">The lock that keeps two calls with one key apart.</param>
    /// <param name="shellSettings">The tenant.</param>
    public AcpMcpIdempotency(IDistributedCache cache, IDistributedLock distributedLock, ShellSettings shellSettings)
    {
        _cache = cache;
        _distributedLock = distributedLock;
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Runs a mutating call once per idempotency key, or replays its first answer.
    /// </summary>
    /// <param name="platformId">The signed-in platform.</param>
    /// <param name="tool">The tool's name.</param>
    /// <param name="checkoutId">The checkout session's id, or <see langword="null"/>.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="payload">The call's payload, whose canonical JSON a retry must match.</param>
    /// <param name="run">Runs the call.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The answer, and whether it was replayed.</returns>
    public async Task<(AcpOperationResult Result, bool Replayed)> RunAsync(
        string platformId,
        string tool,
        string checkoutId,
        string key,
        JsonNode payload,
        Func<Task<AcpOperationResult>> run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (string.IsNullOrWhiteSpace(key))
        {
            return (AcpOperationResult.Error(StatusCodes.Status400BadRequest, "idempotency_key_required", "Send meta.idempotency_key with every call that changes a checkout session."), false);
        }

        if (key.Length > AcpEndpoints.Idempotency.MaxKeyLength)
        {
            return (AcpOperationResult.Error(StatusCodes.Status400BadRequest, "invalid_request", "An idempotency key is 1 to 255 characters."), false);
        }

        var cacheKey = GetCacheKey(_shellSettings.Name, platformId, tool, checkoutId, key);
        var fingerprint = AcpEndpoints.ComputeFingerprint(payload);

        if (await _cache.GetStringAsync(cacheKey, cancellationToken) is { } stored)
        {
            return Replay(stored, fingerprint);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(cacheKey, TimeSpan.FromMilliseconds(500), TimeSpan.FromMinutes(2));

        if (!locked)
        {
            return (AcpOperationResult.Error(StatusCodes.Status409Conflict, "idempotency_in_flight", "A request with this idempotency key is still running.", retryAfterSeconds: 1), false);
        }

        await using (locker)
        {
            if (await _cache.GetStringAsync(cacheKey, cancellationToken) is { } storedMeanwhile)
            {
                return Replay(storedMeanwhile, fingerprint);
            }

            var result = await run();

            if (IsFinal(result))
            {
                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(new StoredAnswer(result.StatusCode, result.Body?.ToJsonString(), fingerprint)),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = AcpEndpoints.Idempotency.Lifetime },
                    cancellationToken);
            }

            return (result, false);
        }
    }

    /// <summary>
    /// Returns whether an answer is the call's final one, kept for a retry; otherwise a retry is run afresh.
    /// </summary>
    /// <param name="result">The answer.</param>
    internal static bool IsFinal(AcpOperationResult result)
        => result.StatusCode < StatusCodes.Status500InternalServerError &&
            result.StatusCode != StatusCodes.Status429TooManyRequests &&
            result.RetryAfterSeconds is null;

    internal static string GetCacheKey(string tenant, string platformId, string tool, string checkoutId, string key)
        => "acp-mcp-idempotency:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenant}|{platformId}|{tool}|{checkoutId}|{key}")));

    private static (AcpOperationResult Result, bool Replayed) Replay(string stored, string fingerprint)
    {
        var answer = JsonSerializer.Deserialize<StoredAnswer>(stored);

        if (!string.Equals(answer.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return (AcpOperationResult.Error(StatusCodes.Status422UnprocessableEntity, "idempotency_conflict", "This idempotency key was used with a different request."), false);
        }

        return (new AcpOperationResult
        {
            StatusCode = answer.StatusCode,
            Body = answer.Body is null ? null : JsonNode.Parse(answer.Body) as JsonObject,
        }, true);
    }

    private sealed record StoredAnswer(int StatusCode, string Body, string Fingerprint);
}
