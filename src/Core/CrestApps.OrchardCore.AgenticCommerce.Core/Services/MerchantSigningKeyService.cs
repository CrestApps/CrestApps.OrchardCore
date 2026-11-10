using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Documents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Keeps the store's signing keys in a document, the private halves protected.
/// </summary>
public sealed class MerchantSigningKeyService : IMerchantSigningKeyService
{
    /// <summary>
    /// How long a key signs before a new one replaces it.
    /// </summary>
    public static readonly TimeSpan RotationAge = TimeSpan.FromDays(90);

    /// <summary>
    /// How long a key that stopped signing stays published.
    /// </summary>
    public static readonly TimeSpan RetiredLifetime = TimeSpan.FromDays(7);

    private const string ProtectionPurpose = "CrestApps.OrchardCore.AgenticCommerce.SigningKeys";

    private readonly IDocumentManager<MerchantSigningKeysDocument> _documents;
    private readonly IDataProtector _protector;
    private readonly IClock _clock;

    // The document as this request last changed it: the immutable copy is cached for the request and doesn't see it.
    private MerchantSigningKeysDocument _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MerchantSigningKeyService"/> class.
    /// </summary>
    /// <param name="documents">The document manager.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="clock">The clock.</param>
    public MerchantSigningKeyService(
        IDocumentManager<MerchantSigningKeysDocument> documents,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock)
    {
        _documents = documents;
        _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<EcJsonWebKey> GetSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        var document = await ReadAsync();
        var current = document.Keys?.LastOrDefault(key => key.RetiredUtc is null);

        if (current is null)
        {
            await RotateAsync(cancellationToken);
            current = _changed.Keys.Last(key => key.RetiredUtc is null);
        }

        return EcJsonWebKey.FromJson(JsonNode.Parse(_protector.Unprotect(current.ProtectedPrivateJwk)) as JsonObject);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EcJsonWebKey>> GetPublishedKeysAsync(CancellationToken cancellationToken = default)
    {
        var document = await ReadAsync();

        if (document.Keys is not { Count: > 0 } || document.Keys.All(key => key.RetiredUtc is not null))
        {
            await GetSigningKeyAsync(cancellationToken);
            document = _changed;
        }

        var cutoff = _clock.UtcNow - RetiredLifetime;

        return document.Keys
            .Where(key => key.RetiredUtc is null || key.RetiredUtc > cutoff)
            .Select(key => EcJsonWebKey.FromJson(JsonNode.Parse(key.PublicJwk) as JsonObject))
            .Where(key => key is not null)
            .ToList();
    }

    /// <inheritdoc />
    public async Task RotateAsync(CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetOrCreateMutableAsync();
        var now = _clock.UtcNow;

        foreach (var key in document.Keys ?? [])
        {
            key.RetiredUtc ??= now;
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var kid = $"merchant-{now:yyyyMMdd}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        var privateKey = EcJsonWebKey.FromECDsa(ecdsa, kid, includePrivateKey: true);

        privateKey.Use = "sig";
        privateKey.Alg = "ES256";

        var publicJwk = privateKey.ToJson(includePrivateKey: false);

        document.Keys ??= [];
        document.Keys.Add(new MerchantSigningKey
        {
            Kid = kid,
            ProtectedPrivateJwk = _protector.Protect(privateKey.ToJson(includePrivateKey: true).ToJsonString()),
            PublicJwk = publicJwk.ToJsonString(),
            CreatedUtc = now,
        });

        Prune(document, now);

        await _documents.UpdateAsync(document);

        _changed = document;
    }

    /// <inheritdoc />
    public async Task<bool> RotateIfDueAsync(CancellationToken cancellationToken = default)
    {
        var document = await ReadAsync();
        var current = document.Keys?.LastOrDefault(key => key.RetiredUtc is null);

        if (current is null || current.CreatedUtc + RotationAge <= _clock.UtcNow)
        {
            await RotateAsync(cancellationToken);

            return true;
        }

        return false;
    }

    private async Task<MerchantSigningKeysDocument> ReadAsync()
        => _changed ?? await _documents.GetOrCreateImmutableAsync();

    // Keys retired long ago verify nothing any more; only the recent ones are kept.
    private static void Prune(MerchantSigningKeysDocument document, DateTime now)
        => document.Keys.RemoveAll(key => key.RetiredUtc is not null && key.RetiredUtc < now - (RetiredLifetime * 4));
}
