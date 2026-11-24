using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.SigningKeys;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// The store's ES256 signing keys on the shared signing keys (<see cref="ISigningKeyRing"/>), key set
/// <see cref="KeySet"/>: the key pairs are secrets of Orchard Core Secrets. Keys the store made before the move are
/// imported once by <see cref="MerchantSigningKeyImportSource"/>, with their ids and dates.
/// </summary>
public sealed class MerchantSigningKeyService : IMerchantSigningKeyService
{
    /// <summary>
    /// The name of the store's key set.
    /// </summary>
    public const string KeySet = "AgenticCommerce.Merchant";

    /// <summary>
    /// How long a key signs before a new one replaces it.
    /// </summary>
    public static readonly TimeSpan RotationAge = TimeSpan.FromDays(90);

    /// <summary>
    /// How long a key that stopped signing stays published.
    /// </summary>
    public static readonly TimeSpan RetiredLifetime = TimeSpan.FromDays(7);

    private readonly ISigningKeyRing _ring;

    /// <summary>
    /// Initializes a new instance of the <see cref="MerchantSigningKeyService"/> class.
    /// </summary>
    /// <param name="ring">The shared signing keys.</param>
    public MerchantSigningKeyService(ISigningKeyRing ring)
    {
        _ring = ring;
    }

    /// <summary>
    /// Registers the store's key set: ES256, rotated every 90 days, a retired key published for 7 days.
    /// </summary>
    /// <param name="options">The key set.</param>
    public static void Configure(SigningKeySetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Algorithm = SigningKeyAlgorithms.ES256;
        options.RotationAge = RotationAge;
        options.RetiredLifetime = RetiredLifetime;
        options.KeyIdPrefix = "merchant";
        options.Description = "Agentic commerce: the store's signing key for AI agents. Made and rotated by the site; do not edit.";
    }

    /// <inheritdoc />
    public async Task<EcJsonWebKey> GetSigningKeyAsync(CancellationToken cancellationToken = default)
        => EcJsonWebKey.FromJson((await _ring.GetSigningKeyAsync(KeySet, cancellationToken)).PrivateJwk);

    /// <inheritdoc />
    public async Task<IReadOnlyList<EcJsonWebKey>> GetPublishedKeysAsync(CancellationToken cancellationToken = default)
        => (await _ring.GetPublishedKeysAsync(KeySet, cancellationToken))
            .Select(EcJsonWebKey.FromJson)
            .Where(key => key is not null)
            .ToList();

    /// <inheritdoc />
    public Task RotateAsync(CancellationToken cancellationToken = default)
        => _ring.RotateAsync(KeySet, cancellationToken);

    /// <inheritdoc />
    public Task<bool> RotateIfDueAsync(CancellationToken cancellationToken = default)
        => _ring.RotateIfDueAsync(KeySet, cancellationToken);
}
