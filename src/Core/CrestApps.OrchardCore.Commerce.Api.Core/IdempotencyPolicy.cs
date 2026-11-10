using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Commerce.Api;

/// <summary>
/// How <see cref="IdempotencyEndpointFilter"/> treats an endpoint's idempotency keys, when the endpoint follows a
/// protocol with rules of its own, such as an agentic commerce protocol. Endpoints without a policy keep the commerce
/// API's rules.
/// </summary>
public sealed class IdempotencyPolicy
{
    /// <summary>
    /// Gets the header the key comes in.
    /// </summary>
    public string KeyHeader { get; init; } = CommerceApi.IdempotencyKeyHeader;

    /// <summary>
    /// Gets how long an answer is kept for a retry.
    /// </summary>
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets the longest key accepted.
    /// </summary>
    public int MaxKeyLength { get; init; } = IdempotencyEndpointFilter.MaxKeyLength;

    /// <summary>
    /// Gets what a request without a key gets, or <see langword="null"/> to run it without one.
    /// </summary>
    public Func<HttpContext, IResult> MissingKey { get; init; }

    /// <summary>
    /// Gets what a request with a key that is too long gets, or <see langword="null"/> for the commerce API's answer.
    /// </summary>
    public Func<HttpContext, IResult> InvalidKey { get; init; }

    /// <summary>
    /// Gets the fingerprint of a request's body, which a retry with the same key must match, or <see langword="null"/>
    /// to not compare bodies.
    /// </summary>
    public Func<HttpContext, string> Fingerprint { get; init; }

    /// <summary>
    /// Gets what a request with a used key and a different body gets.
    /// </summary>
    public Func<HttpContext, IResult> Conflict { get; init; }

    /// <summary>
    /// Gets what a request gets while the first request with its key still runs, or <see langword="null"/> for the
    /// commerce API's answer.
    /// </summary>
    public Func<HttpContext, IResult> InFlight { get; init; }

    /// <summary>
    /// Gets the caller a key belongs to, or <see langword="null"/> for the signed-in user or the guest's token.
    /// </summary>
    public Func<HttpContext, string> Caller { get; init; }

    /// <summary>
    /// Gets a header set to <c>true</c> on a replayed answer, such as ACP's <c>Idempotent-Replayed</c>.
    /// </summary>
    public string ReplayedHeader { get; init; }

    /// <summary>
    /// Gets a value indicating whether the key is sent back in the answer's own header.
    /// </summary>
    public bool EchoKey { get; init; }
}
