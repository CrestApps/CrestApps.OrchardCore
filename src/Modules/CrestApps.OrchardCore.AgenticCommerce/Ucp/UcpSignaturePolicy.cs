using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// What a UCP request signature must cover to be accepted (UCP signatures specification): the method, authority and
/// path, so it can't be sent to another target or another store; the body's digest when there is a body; the
/// idempotency key when the request has one, which is what UCP relies on against replays. A <c>created</c> time, when
/// the signer sends one, is within five minutes of the store's clock, and an <c>expires</c> time hasn't passed.
/// </summary>
public static class UcpSignaturePolicy
{
    /// <summary>
    /// How far a signature's <c>created</c> time may be from the store's clock, either way.
    /// </summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long an accepted signature is remembered, so it isn't accepted twice.
    /// </summary>
    public static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(10);

    private static readonly string[] _requiredComponents = ["@method", "@authority", "@path"];

    /// <summary>
    /// Returns why a verified signature isn't accepted, or <see langword="null"/>.
    /// </summary>
    /// <param name="verification">The verified signature.</param>
    /// <param name="hasBody">Whether the request has a body.</param>
    /// <param name="idempotencyKey">The request's <c>Idempotency-Key</c>, or <see langword="null"/>.</param>
    /// <param name="now">The store's clock.</param>
    public static string Check(HttpSignatureVerification verification, bool hasBody, string idempotencyKey, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(verification);

        var covered = new HashSet<string>(verification.CoveredComponents ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var component in _requiredComponents)
        {
            if (!covered.Contains(component))
            {
                return $"The signature must cover {component}.";
            }
        }

        if (hasBody && !covered.Contains("content-digest"))
        {
            return "The signature must cover content-digest.";
        }

        if (!string.IsNullOrEmpty(idempotencyKey) && !covered.Contains("idempotency-key"))
        {
            return "The signature must cover idempotency-key.";
        }

        if (verification.Created is { } created && (now - created).Duration() > MaxClockSkew)
        {
            return "The signature's created time is more than five minutes from the store's clock.";
        }

        if (verification.Expires is { } expires && expires <= now)
        {
            return "The signature has expired.";
        }

        return null;
    }
}
