using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// What an AP2 mandate is verified against.
/// </summary>
public sealed class Ap2VerificationOptions
{
    /// <summary>
    /// Gets or sets the resolver of trusted root keys (credential issuers or agent providers). It is called with
    /// <c>{ "header": {...}, "payload": {...} }</c>, the decoded header and payload of the root SD-JWT, so it can pick
    /// the key by <c>kid</c> or <c>iss</c>, and returns <see langword="null"/> when the root is not trusted.
    /// </summary>
    public Func<JsonObject, EcJsonWebKey> RootKeyResolver { get; set; }

    /// <summary>
    /// Gets or sets the trusted root certificates for a root SD-JWT that carries an <c>x5c</c> header. When it is
    /// empty, an <c>x5c</c> header is ignored and <see cref="RootKeyResolver"/> is used.
    /// </summary>
    public IReadOnlyList<X509Certificate2> TrustedRoots { get; set; }

    /// <summary>
    /// Gets or sets the audience the terminal hop must carry (<c>aud</c>), or <see langword="null"/> to not check it.
    /// </summary>
    public string ExpectedAudience { get; set; }

    /// <summary>
    /// Gets or sets the nonce the terminal hop must carry, or <see langword="null"/> to not check it.
    /// </summary>
    public string ExpectedNonce { get; set; }

    /// <summary>
    /// Gets or sets the expected <c>checkout_hash</c> of the closed mandate, or <see langword="null"/> to not check it.
    /// </summary>
    public string ExpectedCheckoutHash { get; set; }

    /// <summary>
    /// Gets or sets the resolver of the merchant's own public keys by <c>kid</c>. When it is set, the signature of the
    /// <c>checkout_jwt</c> in the closed mandate is verified too.
    /// </summary>
    public Func<string, EcJsonWebKey> MerchantKeyResolver { get; set; }

    /// <summary>
    /// Gets or sets the current time, which <c>iat</c> and <c>exp</c> are checked against.
    /// </summary>
    public DateTimeOffset Now { get; set; }

    /// <summary>
    /// Gets or sets the allowed clock skew for <c>iat</c> and <c>exp</c>. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(60);
}
