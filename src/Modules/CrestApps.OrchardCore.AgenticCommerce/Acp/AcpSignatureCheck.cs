using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// Applies a platform's request signing settings to an ACP request: a platform that requires signed requests must sign
/// every one; any other platform with a public key may sign, and a request it signs must verify. A platform on signature
/// version 2 signs the timestamp, method and path with the body; one saved before versions existed signs the body only.
/// </summary>
public static class AcpSignatureCheck
{
    /// <summary>
    /// The ACP error code of a request whose signature is missing or doesn't verify.
    /// </summary>
    public const string ErrorCode = "invalid_signature";

    /// <summary>
    /// The method a version 2 signature of an MCP tool call is made with; the tool's name is its path.
    /// </summary>
    public const string McpMethod = "MCP";

    /// <summary>
    /// Returns why a request's signature isn't accepted, or <see langword="null"/> when it is (or isn't needed). Without
    /// a method and a path, a platform on signature version 2 is refused.
    /// </summary>
    /// <param name="platform">The signed-in platform.</param>
    /// <param name="input">The signed bytes (the canonical JSON of the body), or <see langword="null"/> when the body isn't JSON.</param>
    /// <param name="signature">The <c>Signature</c> header or <c>meta.signature</c>.</param>
    /// <param name="timestamp">The <c>Timestamp</c> header or <c>meta.timestamp</c>.</param>
    /// <param name="now">The current time.</param>
    public static string Check(AgentPlatform platform, byte[] input, string signature, string timestamp, DateTimeOffset now)
        => Check(platform, input, signature, timestamp, method: null, path: null, now);

    /// <summary>
    /// Returns why a request's signature isn't accepted, or <see langword="null"/> when it is (or isn't needed).
    /// </summary>
    /// <param name="platform">The signed-in platform.</param>
    /// <param name="input">The signed bytes (the canonical JSON of the body), or <see langword="null"/> when the body isn't JSON.</param>
    /// <param name="signature">The <c>Signature</c> header or <c>meta.signature</c>.</param>
    /// <param name="timestamp">The <c>Timestamp</c> header or <c>meta.timestamp</c>.</param>
    /// <param name="method">The request's method, such as <c>POST</c>, or <see cref="McpMethod"/> for an MCP tool call.</param>
    /// <param name="path">The request's path, or the MCP tool's name.</param>
    /// <param name="now">The current time.</param>
    public static string Check(AgentPlatform platform, byte[] input, string signature, string timestamp, string method, string path, DateTimeOffset now)
        => Check(platform, input, signature, timestamp, method, path, now, out _);

    /// <summary>
    /// Returns why a request's signature isn't accepted, or <see langword="null"/> when it is (or isn't needed), with the
    /// bytes a verified signature bound to its timestamp was made over, for the replay guard.
    /// </summary>
    /// <param name="platform">The signed-in platform.</param>
    /// <param name="input">The signed bytes (the canonical JSON of the body), or <see langword="null"/> when the body isn't JSON.</param>
    /// <param name="signature">The <c>Signature</c> header or <c>meta.signature</c>.</param>
    /// <param name="timestamp">The <c>Timestamp</c> header or <c>meta.timestamp</c>.</param>
    /// <param name="method">The request's method, such as <c>POST</c>, or <see cref="McpMethod"/> for an MCP tool call.</param>
    /// <param name="path">The request's path, or the MCP tool's name and the checkout's id.</param>
    /// <param name="now">The current time.</param>
    /// <param name="verified">The bytes a version 2 signature was verified over; <see langword="null"/> when nothing was verified, or for a version 1 signature, which signs no time and so can't be made single-use.</param>
    public static string Check(AgentPlatform platform, byte[] input, string signature, string timestamp, string method, string path, DateTimeOffset now, out byte[] verified)
    {
        ArgumentNullException.ThrowIfNull(platform);

        verified = null;

        var signed = !string.IsNullOrWhiteSpace(signature) || !string.IsNullOrWhiteSpace(timestamp);
        var hasKey = !string.IsNullOrWhiteSpace(platform.AcpSignaturePublicKey);

        if (!platform.AcpRequireSignedRequests && (!signed || !hasKey))
        {
            return null;
        }

        if (!signed)
        {
            return "Sign the request: send the Signature and Timestamp headers.";
        }

        if (!IsSupported(platform.AcpSignatureAlgorithm) || !AcpRequestSignature.TryReadPublicKey(platform.AcpSignaturePublicKey, out var key))
        {
            return "The store can't verify the platform's signatures: its public key isn't set up.";
        }

        var bound = GetVersion(platform) >= AcpRequestSignature.BoundVersion && input is not null;

        if (bound)
        {
            if (string.IsNullOrEmpty(method) || string.IsNullOrEmpty(path))
            {
                return "The signature doesn't match the request.";
            }

            input = AcpRequestSignature.GetBoundSigningInput(timestamp, method, path, input);
        }

        var verification = AcpRequestSignature.Verify(input, signature, timestamp, key, GetClockSkew(platform), now);

        verified = verification == AcpSignatureVerification.Valid && bound ? input : null;

        return verification switch
        {
            AcpSignatureVerification.Valid => null,
            AcpSignatureVerification.MissingHeaders => "Send both the Signature and the Timestamp headers.",
            AcpSignatureVerification.InvalidTimestamp => "The Timestamp isn't an RFC 3339 date and time.",
            AcpSignatureVerification.StaleTimestamp => "The Timestamp is too far from the store's clock.",
            _ => "The signature doesn't match the request.",
        };
    }

    /// <summary>
    /// Remembers a verified signature for the time its timestamp could be valid, by the bytes it was verified over, and
    /// returns <see cref="AgentSignatureUse.Replayed"/> when it was used before by a request that isn't the same idempotent
    /// one.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="platform">The signed-in platform.</param>
    /// <param name="verified">The bytes <see cref="Check(AgentPlatform, byte[], string, string, string, string, DateTimeOffset, out byte[])"/> verified, or <see langword="null"/> for nothing to remember.</param>
    /// <param name="idempotencyKey">The request's idempotency key when an idempotency layer answers it, or <see langword="null"/>.</param>
    public static Task<AgentSignatureUse> UseAsync(HttpContext httpContext, AgentPlatform platform, byte[] verified, string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(platform);

        if (verified is null || httpContext.RequestServices.GetService<AgentSignatureReplayGuard>() is not { } guard)
        {
            return Task.FromResult(AgentSignatureUse.Accepted);
        }

        // A timestamp is accepted for the skew either way, so a signature could be valid for twice that.
        return guard.UseAsync(platform.ItemId, verified, idempotencyKey, GetClockSkew(platform) * 2, httpContext.RequestAborted);
    }

    /// <summary>
    /// Returns the answer to a signature the replay guard didn't accept: <c>401</c> for a replay, or a retryable
    /// <c>503</c> when the guard couldn't tell (its lock store is down or busy).
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="use">What the guard answered.</param>
    public static AcpOperationResult RefusedUse(HttpContext httpContext, AgentSignatureUse use)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (use == AgentSignatureUse.Unavailable)
        {
            httpContext.Response.Headers.RetryAfter = "1";

            return AcpOperationResult.Error(StatusCodes.Status503ServiceUnavailable, "service_unavailable", "The signature couldn't be checked right now. Send the request again.");
        }

        return AcpOperationResult.Error(StatusCodes.Status401Unauthorized, ErrorCode, "The signature was used before. Sign each request anew.");
    }

    /// <summary>
    /// Returns the signature version of a platform: 1 when it has none.
    /// </summary>
    /// <param name="platform">The platform.</param>
    public static int GetVersion(AgentPlatform platform)
        => platform?.AcpSignatureVersion ?? AcpRequestSignature.BodyOnlyVersion;

    /// <summary>
    /// Returns the clock skew a platform's timestamps may have.
    /// </summary>
    /// <param name="platform">The platform.</param>
    public static TimeSpan GetClockSkew(AgentPlatform platform)
        => platform?.AcpSignatureClockSkewSeconds is int seconds && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : AcpRequestSignature.DefaultClockSkew;

    /// <summary>
    /// Returns whether the store verifies an algorithm; empty means ES256.
    /// </summary>
    /// <param name="algorithm">The algorithm's name.</param>
    public static bool IsSupported(string algorithm)
        => string.IsNullOrWhiteSpace(algorithm) || AcpRequestSignature.Algorithms.Contains(algorithm.Trim(), StringComparer.Ordinal);
}
