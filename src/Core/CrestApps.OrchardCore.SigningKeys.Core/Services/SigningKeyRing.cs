using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.SigningKeys.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Documents;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.SigningKeys.Core.Services;

/// <summary>
/// The key ring of the shared signing keys. The key pairs are secrets of Orchard Core Secrets, named
/// <c>SigningKeys.{key set}.{kid}</c>; <see cref="SigningKeysDocument"/> keeps each set's key ids, public halves, secret
/// names and dates. A key is made, rotated or imported under a distributed lock per set, so two requests never make two
/// keys at once.
/// </summary>
public sealed class SigningKeyRing : ISigningKeyRing
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IDocumentManager<SigningKeysDocument> _documents;
    private readonly ISecretManager _secretManager;
    private readonly IDistributedLock _distributedLock;
    private readonly SigningKeyOptions _options;
    private readonly IEnumerable<ISigningKeyImportSource> _importSources;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    // The private keys this scope already read or made: a secret saved in this request is not readable from Secrets'
    // cached document until the request ends.
    private readonly Dictionary<string, SigningKey> _keys = new(StringComparer.Ordinal);

    // The document as this scope last changed it: the immutable copy is cached for the request and does not see it.
    private SigningKeysDocument _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningKeyRing"/> class.
    /// </summary>
    /// <param name="documents">The document of key records.</param>
    /// <param name="secretManager">Orchard Core Secrets, which keeps the key pairs.</param>
    /// <param name="distributedLock">The distributed lock that makes one key at a time per set.</param>
    /// <param name="options">The registered key sets.</param>
    /// <param name="importSources">The keys modules kept before they used the ring.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SigningKeyRing(
        IDocumentManager<SigningKeysDocument> documents,
        ISecretManager secretManager,
        IDistributedLock distributedLock,
        IOptions<SigningKeyOptions> options,
        IEnumerable<ISigningKeyImportSource> importSources,
        IClock clock,
        ILogger<SigningKeyRing> logger)
    {
        _documents = documents;
        _secretManager = secretManager;
        _distributedLock = distributedLock;
        _options = options.Value;
        _importSources = importSources;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SigningKey> GetSigningKeyAsync(string keySet, CancellationToken cancellationToken = default)
    {
        var options = GetKeySet(keySet);
        var current = Current(await ReadAsync(), keySet);

        if (current is not null && await LoadAsync(options, current) is { } key)
        {
            return key;
        }

        return await WithLockAsync(keySet, async () =>
        {
            // Another request may have made the key while this one waited for the lock.
            var document = await _documents.GetOrCreateMutableAsync();
            var latest = await CurrentOrImportedAsync(options, document);

            if (latest is not null && await LoadAsync(options, latest) is { } made)
            {
                return made;
            }

            if (latest is not null)
            {
                _logger.LogWarning("The secret of the signing key '{Kid}' of the key set '{KeySet}' is missing or cannot be read; a new key is made.", latest.Kid, keySet);
            }

            return await RotateCoreAsync(options, document);
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JsonObject>> GetPublishedKeysAsync(string keySet, CancellationToken cancellationToken = default)
    {
        GetKeySet(keySet);

        var document = await ReadAsync();

        if (Current(document, keySet) is null)
        {
            await GetSigningKeyAsync(keySet, cancellationToken);
            document = await ReadAsync();
        }

        var cutoff = _clock.UtcNow - GetKeySet(keySet).RetiredLifetime;

        return Records(document, keySet)
            .Where(record => record.RetiredUtc is null || record.RetiredUtc > cutoff)
            .Select(record => JsonNode.Parse(record.PublicJwk) as JsonObject)
            .Where(jwk => jwk is not null)
            .ToList();
    }

    /// <inheritdoc />
    public Task<SigningKey> RotateAsync(string keySet, CancellationToken cancellationToken = default)
    {
        var options = GetKeySet(keySet);

        return WithLockAsync(keySet, async () =>
        {
            var document = await _documents.GetOrCreateMutableAsync();

            await CurrentOrImportedAsync(options, document);

            return await RotateCoreAsync(options, document);
        });
    }

    /// <inheritdoc />
    public async Task<bool> RotateIfDueAsync(string keySet, CancellationToken cancellationToken = default)
    {
        var options = GetKeySet(keySet);
        var current = Current(await ReadAsync(), keySet);

        if (current is not null && current.CreatedUtc + options.RotationAge > _clock.UtcNow)
        {
            return false;
        }

        return await WithLockAsync(keySet, async () =>
        {
            var document = await _documents.GetOrCreateMutableAsync();
            var latest = await CurrentOrImportedAsync(options, document);

            if (latest is not null && latest.CreatedUtc + options.RotationAge > _clock.UtcNow)
            {
                return false;
            }

            await RotateCoreAsync(options, document);

            return true;
        });
    }

    /// <inheritdoc />
    public Task<int> ImportAsync(string keySet, IEnumerable<SigningKeyImport> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var options = GetKeySet(keySet);

        return WithLockAsync(keySet, async () =>
        {
            var document = await _documents.GetOrCreateMutableAsync();
            var imported = await ImportCoreAsync(options, document, keys);

            if (imported > 0)
            {
                await SaveAsync(document);
            }

            return imported;
        });
    }

    /// <inheritdoc />
    public async Task<bool> HasKeysAsync(string keySet, CancellationToken cancellationToken = default)
    {
        GetKeySet(keySet);

        return Records(await ReadAsync(), keySet).Count > 0;
    }

    // The current key, after importing the keys of the set's import sources when the set has none yet. Saves the
    // document when it imported a key.
    private async Task<SigningKeyRecord> CurrentOrImportedAsync(SigningKeySetOptions options, SigningKeysDocument document)
    {
        if (Records(document, options.Name).Count > 0)
        {
            return Current(document, options.Name);
        }

        var imported = 0;

        foreach (var source in _importSources.Where(source => string.Equals(source.KeySet, options.Name, StringComparison.Ordinal)))
        {
            imported += await ImportCoreAsync(options, document, await source.GetKeysAsync());
        }

        if (imported > 0)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("{Count} signing keys were imported into the key set '{KeySet}'.", imported, options.Name);
            }

            await SaveAsync(document);
        }

        return Current(document, options.Name);
    }

    private async Task<int> ImportCoreAsync(SigningKeySetOptions options, SigningKeysDocument document, IEnumerable<SigningKeyImport> keys)
    {
        var records = GetOrAddRecords(document, options.Name);
        var imported = 0;

        foreach (var key in (keys ?? []).Where(key => key?.PrivateJwk is not null && !string.IsNullOrEmpty(key.Kid)).OrderBy(key => key.CreatedUtc))
        {
            if (records.Any(record => string.Equals(record.Kid, key.Kid, StringComparison.Ordinal)))
            {
                continue;
            }

            var secretName = SecretName(options.Name, key.Kid);

            await _secretManager.SaveSecretAsync(secretName, SigningKeyJwk.ToSecret(options.Algorithm, key.PrivateJwk), SaveOptions(options, key.Kid));

            records.Add(new SigningKeyRecord
            {
                Kid = key.Kid,
                Algorithm = options.Algorithm,
                SecretName = secretName,
                PublicJwk = (key.PublicJwk ?? SigningKeyJwk.PublicOf(key.PrivateJwk)).ToJsonString(),
                CreatedUtc = key.CreatedUtc,
                RetiredUtc = key.RetiredUtc,
            });

            imported++;
        }

        if (imported > 0)
        {
            // Only the newest key that has not retired signs.
            records.Sort((left, right) => left.CreatedUtc.CompareTo(right.CreatedUtc));

            var signing = records.LastOrDefault(record => record.RetiredUtc is null);

            foreach (var record in records.Where(record => record.RetiredUtc is null && !ReferenceEquals(record, signing)))
            {
                record.RetiredUtc = _clock.UtcNow;
            }
        }

        return imported;
    }

    private async Task<SigningKey> RotateCoreAsync(SigningKeySetOptions options, SigningKeysDocument document)
    {
        var now = _clock.UtcNow;
        var records = GetOrAddRecords(document, options.Name);

        foreach (var record in records)
        {
            record.RetiredUtc ??= now;
        }

        var kid = $"{options.KeyIdPrefix}-{now:yyyyMMdd}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        var privateJwk = SigningKeyJwk.Create(options.Algorithm, kid);
        var publicJwk = SigningKeyJwk.PublicOf(privateJwk);
        var secretName = SecretName(options.Name, kid);

        // The secret first: a record never names a secret that was not saved.
        await _secretManager.SaveSecretAsync(secretName, SigningKeyJwk.ToSecret(options.Algorithm, privateJwk), SaveOptions(options, kid));

        records.Add(new SigningKeyRecord
        {
            Kid = kid,
            Algorithm = options.Algorithm,
            SecretName = secretName,
            PublicJwk = publicJwk.ToJsonString(),
            CreatedUtc = now,
        });

        // Keys retired long ago verify nothing any more.
        foreach (var expired in records.Where(record => record.RetiredUtc is not null && record.RetiredUtc < now - (options.RetiredLifetime * 4)).ToList())
        {
            await _secretManager.RemoveSecretAsync(expired.SecretName);
            records.Remove(expired);
        }

        await SaveAsync(document);

        var key = new SigningKey
        {
            Kid = kid,
            Algorithm = options.Algorithm,
            PrivateJwk = privateJwk,
            PublicJwk = publicJwk,
            CreatedUtc = now,
        };

        _keys[CacheKey(options.Name, kid)] = key;

        return key;
    }

    private async Task<SigningKey> LoadAsync(SigningKeySetOptions options, SigningKeyRecord record)
    {
        if (_keys.TryGetValue(CacheKey(options.Name, record.Kid), out var cached))
        {
            return cached;
        }

        var secret = record.Algorithm == SigningKeyAlgorithms.ES256
            ? (ISecret)await _secretManager.GetSecretAsync<TextSecret>(record.SecretName)
            : await _secretManager.GetSecretAsync<RsaKeySecret>(record.SecretName);

        if (secret is null || SigningKeyJwk.FromSecret(record.Algorithm, secret, record.Kid) is not { } privateJwk)
        {
            return null;
        }

        var key = new SigningKey
        {
            Kid = record.Kid,
            Algorithm = record.Algorithm,
            PrivateJwk = privateJwk,
            PublicJwk = JsonNode.Parse(record.PublicJwk) as JsonObject ?? SigningKeyJwk.PublicOf(privateJwk),
            CreatedUtc = record.CreatedUtc,
        };

        _keys[CacheKey(options.Name, record.Kid)] = key;

        return key;
    }

    private async Task<T> WithLockAsync<T>(string keySet, Func<Task<T>> work)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync("CrestApps.SigningKeys:" + keySet, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            throw new TimeoutException($"The signing keys of the key set '{keySet}' are being changed by another request.");
        }

        await using (locker)
        {
            return await work();
        }
    }

    private async Task SaveAsync(SigningKeysDocument document)
    {
        await _documents.UpdateAsync(document);

        _changed = document;
    }

    private async Task<SigningKeysDocument> ReadAsync()
        => _changed ?? await _documents.GetOrCreateImmutableAsync();

    private SigningKeySetOptions GetKeySet(string keySet)
    {
        ArgumentException.ThrowIfNullOrEmpty(keySet);

        if (!_options.KeySets.TryGetValue(keySet, out var options))
        {
            throw new InvalidOperationException($"No feature registered the signing key set '{keySet}' with AddSigningKeySet.");
        }

        if (!SigningKeyAlgorithms.IsSupported(options.Algorithm))
        {
            throw new InvalidOperationException($"The signing key set '{keySet}' asks for the algorithm '{options.Algorithm}', which is not supported.");
        }

        return options;
    }

    private static SigningKeyRecord Current(SigningKeysDocument document, string keySet)
        => Records(document, keySet).LastOrDefault(record => record.RetiredUtc is null);

    private static List<SigningKeyRecord> Records(SigningKeysDocument document, string keySet)
        => document?.KeySets is not null && document.KeySets.TryGetValue(keySet, out var records) && records is not null ? records : [];

    private static List<SigningKeyRecord> GetOrAddRecords(SigningKeysDocument document, string keySet)
    {
        document.KeySets ??= new(StringComparer.Ordinal);

        if (!document.KeySets.TryGetValue(keySet, out var records) || records is null)
        {
            records = [];
            document.KeySets[keySet] = records;
        }

        return records;
    }

    private static SecretSaveOptions SaveOptions(SigningKeySetOptions options, string kid)
        => new()
        {
            Description = string.IsNullOrWhiteSpace(options.Description)
                ? $"Signing key {kid} of {options.Name}. Made and rotated by the site; do not edit."
                : $"{options.Description} ({kid})",
        };

    private static string SecretName(string keySet, string kid)
        => $"SigningKeys.{keySet}.{kid}";

    private static string CacheKey(string keySet, string kid)
        => keySet + "|" + kid;
}
