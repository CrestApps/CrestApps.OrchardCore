using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.SigningKeys;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// The store's signing keys from before the shared signing keys: <see cref="MerchantSigningKeysDocument"/>, with the
/// private halves protected by Data Protection. The ring imports them, once, before it makes the first key of
/// <see cref="MerchantSigningKeyService.KeySet"/>, so the keys keep signing and the published key set stays the same. The
/// old document is left as it is.
/// </summary>
public sealed class MerchantSigningKeyImportSource : ISigningKeyImportSource
{
    /// <summary>
    /// The Data Protection purpose the private halves were protected with.
    /// </summary>
    public const string ProtectionPurpose = "CrestApps.OrchardCore.AgenticCommerce.SigningKeys";

    private readonly IDocumentManager<MerchantSigningKeysDocument> _documents;
    private readonly IDataProtector _protector;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MerchantSigningKeyImportSource"/> class.
    /// </summary>
    /// <param name="documents">The old document of keys.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="logger">The logger.</param>
    public MerchantSigningKeyImportSource(
        IDocumentManager<MerchantSigningKeysDocument> documents,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<MerchantSigningKeyImportSource> logger)
    {
        _documents = documents;
        _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
        _logger = logger;
    }

    /// <inheritdoc />
    public string KeySet
        => MerchantSigningKeyService.KeySet;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SigningKeyImport>> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetOrCreateImmutableAsync();
        var keys = new List<SigningKeyImport>();

        foreach (var key in document?.Keys ?? [])
        {
            try
            {
                if (JsonNode.Parse(_protector.Unprotect(key.ProtectedPrivateJwk)) is not JsonObject privateJwk ||
                    JsonNode.Parse(key.PublicJwk) is not JsonObject publicJwk)
                {
                    continue;
                }

                keys.Add(new SigningKeyImport
                {
                    Kid = key.Kid,
                    PrivateJwk = privateJwk,
                    PublicJwk = publicJwk,
                    CreatedUtc = DateTime.SpecifyKind(key.CreatedUtc, DateTimeKind.Utc),
                    RetiredUtc = key.RetiredUtc is { } retired ? DateTime.SpecifyKind(retired, DateTimeKind.Utc) : null,
                });
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
            {
                _logger.LogWarning(exception, "The store's signing key '{Kid}' could not be read, so it was not moved to the shared signing keys.", key.Kid);
            }
        }

        return keys;
    }
}
