using System.Text.Json.Nodes;
using CrestApps.OrchardCore.SigningKeys;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// The platform's RS256 key, a key set of the shared signing keys (<see cref="LtiConstants.PlatformKeySet"/>): an
/// <c>RsaKeySecret</c> of Orchard Core Secrets, rotated every 90 days, the old key published for 30 more days so a tool
/// that cached the key set still verifies a launch.
/// </summary>
public sealed class LtiPlatformKeys
{
    /// <summary>
    /// How long a platform key signs before a new one replaces it.
    /// </summary>
    public static readonly TimeSpan RotationAge = TimeSpan.FromDays(90);

    /// <summary>
    /// How long a retired platform key stays published.
    /// </summary>
    public static readonly TimeSpan RetiredLifetime = TimeSpan.FromDays(30);

    private readonly ISigningKeyRing _ring;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiPlatformKeys"/> class.
    /// </summary>
    /// <param name="ring">The shared signing keys.</param>
    public LtiPlatformKeys(ISigningKeyRing ring)
    {
        _ring = ring;
    }

    /// <summary>
    /// Registers the platform's key set.
    /// </summary>
    /// <param name="options">The key set.</param>
    public static void Configure(SigningKeySetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Algorithm = SigningKeyAlgorithms.RS256;
        options.RotationAge = RotationAge;
        options.RetiredLifetime = RetiredLifetime;
        options.KeyIdPrefix = "lti";
        options.Description = "External tools: the LTI platform key that signs launches. Made and rotated by the site; do not edit.";
    }

    /// <summary>
    /// Returns the credentials launches are signed with now.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>RS256 credentials whose key carries its <c>kid</c>.</returns>
    public async Task<SigningCredentials> GetSigningCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var key = await _ring.GetSigningKeyAsync(LtiConstants.PlatformKeySet, cancellationToken);

        return new SigningCredentials(new JsonWebKey(key.PrivateJwk.ToJsonString()), SecurityAlgorithms.RsaSha256);
    }

    /// <summary>
    /// Returns the platform's JSON Web Key Set, as it is published.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>An object with a <c>keys</c> array.</returns>
    public async Task<JsonObject> GetJsonWebKeySetAsync(CancellationToken cancellationToken = default)
    {
        var keys = new JsonArray();

        foreach (var key in await _ring.GetPublishedKeysAsync(LtiConstants.PlatformKeySet, cancellationToken))
        {
            keys.Add(key.DeepClone());
        }

        return new JsonObject
        {
            ["keys"] = keys,
        };
    }
}
