using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// Reads and changes the addresses that unsubscribed from survey invitations. Each opt-out is its own record
/// (<see cref="SurveyInvitationOptOut"/>), found by the hash of its address through the index, so a check reads only the
/// records of the address it asks about, however many people unsubscribed, and two unsubscribes never write the same
/// record. Addresses are kept as HMACs keyed by a site key protected with ASP.NET Data Protection, which the small
/// <see cref="SurveyInvitationOptOutDocument"/> holds; plain SHA-256 hashes stored before the key existed are still
/// honored. Two first-ever unsubscribes at the same moment can each make a key: every key is tried, so neither is lost.
/// Hashes the old single list still holds (until <c>SurveyInvitationOptOutMoveStep</c> moves them into records) are
/// honored and removed the same way. When a key can't be read (missing Data Protection keys), an address it might hold
/// is <see cref="SurveyInvitationOptOutState.Unknown"/>: no survey invitation is sent to it and nothing is recorded as an
/// opt-out, and the consent source answers <c>Unknown</c> to other sends.
/// </summary>
internal sealed class SurveyInvitationOptOutStore
{
    /// <summary>
    /// The Data Protection purpose of the key.
    /// </summary>
    public const string Purpose = "CrestApps.OrchardCore.Surveys.InvitationOptOutKey.v1";

    private const string KeyedPrefix = "h1:";

    private readonly ISession _session;
    private readonly IDataProtector _protector;
    private readonly ILogger _logger;

    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    private List<SurveyInvitationOptOutDocument> _documents;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationOptOutStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="dataProtectionProvider">The Data Protection provider, which protects the HMAC key.</param>
    /// <param name="logger">The logger.</param>
    public SurveyInvitationOptOutStore(
        ISession session,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SurveyInvitationOptOutStore> logger)
    {
        _session = session;
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _logger = logger;
    }

    /// <summary>
    /// Gets the plain SHA-256 hash addresses were kept under before the list was keyed.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <returns>The hash, or <see langword="null"/> when there is no address.</returns>
    public static string HashOf(string address)
        => string.IsNullOrWhiteSpace(address) ? null : SurveyRespondentKeys.Hash(Normalize(address));

    /// <summary>
    /// Gets the keyed hash an address is kept under.
    /// </summary>
    /// <param name="key">The HMAC key.</param>
    /// <param name="address">The e-mail address.</param>
    /// <returns>The keyed hash, or <see langword="null"/> when there is no address.</returns>
    public static string KeyedHashOf(byte[] key, string address)
    {
        ArgumentNullException.ThrowIfNull(key);

        return string.IsNullOrWhiteSpace(address)
            ? null
            : KeyedPrefix + Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Normalize(address)))).ToLowerInvariant();
    }

    /// <summary>
    /// Gets what the list says about an address.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether it unsubscribed, did not, or can't be told because a list's key can't be read.</returns>
    public async Task<SurveyInvitationOptOutState> GetStateAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return SurveyInvitationOptOutState.NotOptedOut;
        }

        var (hashes, unknown) = await HashesAsync(address, cancellationToken);

        foreach (var document in await LoadAsync(cancellationToken))
        {
            if (document.AddressHashes.Overlaps(hashes))
            {
                return SurveyInvitationOptOutState.OptedOut;
            }
        }

        if ((await RecordsAsync(hashes, cancellationToken)).Count > 0)
        {
            return SurveyInvitationOptOutState.OptedOut;
        }

        return unknown ? SurveyInvitationOptOutState.Unknown : SurveyInvitationOptOutState.NotOptedOut;
    }

    /// <summary>
    /// Adds or removes an address. The change is saved when the session commits.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <param name="optedOut">Whether the address opted out.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SetAsync(string address, bool optedOut, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        var documents = await LoadAsync(cancellationToken);
        var legacy = HashOf(address);

        if (optedOut)
        {
            if (documents.Count == 0)
            {
                documents.Add(new SurveyInvitationOptOutDocument());
            }

            // New opt-outs are keyed with the oldest key; another key exists only when two first unsubscribes raced.
            var primary = documents[0];

            var newKey = primary.ProtectedKey is null;

            if (newKey)
            {
                primary.ProtectedKey = _protector.Protect(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            }

            // Without the key the address is kept under its plain hash, so the opt-out is still honored.
            var key = Key(primary);
            var hash = key is null ? legacy : KeyedHashOf(key, address);
            var existing = await RecordsAsync([hash, legacy], cancellationToken);

            // Saved after the reads, so this session writes nothing before it commits.
            if (newKey)
            {
                await _session.SaveAsync(primary, checkConcurrency: true, cancellationToken: cancellationToken);
            }

            if (!existing.Any(record => string.Equals(record.AddressHash, hash, StringComparison.Ordinal)))
            {
                await _session.SaveAsync(new SurveyInvitationOptOut { AddressHash = hash }, collection: SurveysConstants.CollectionName, cancellationToken: cancellationToken);
            }

            if (key is not null)
            {
                foreach (var record in existing.Where(record => string.Equals(record.AddressHash, legacy, StringComparison.Ordinal)))
                {
                    _session.Delete(record, SurveysConstants.CollectionName);
                }
            }

            return;
        }

        var (hashes, _) = await HashesAsync(address, cancellationToken);

        foreach (var record in await RecordsAsync(hashes, cancellationToken))
        {
            _session.Delete(record, SurveysConstants.CollectionName);
        }

        // An address the old single list still holds is removed from it too.
        foreach (var document in documents)
        {
            var removed = false;

            foreach (var hash in hashes)
            {
                removed |= document.AddressHashes.Remove(hash);
            }

            if (removed)
            {
                await _session.SaveAsync(document, checkConcurrency: true, cancellationToken: cancellationToken);
            }
        }
    }

    private static string Normalize(string address)
        => address.Trim().ToLowerInvariant();

    // Every hash the address may be kept under: its plain hash, and its keyed hash under each key that can be read.
    // Unknown is set when a key can't be read.
    private async Task<(HashSet<string> Hashes, bool Unknown)> HashesAsync(string address, CancellationToken cancellationToken)
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal) { HashOf(address) };
        var unknown = false;

        foreach (var document in await LoadAsync(cancellationToken))
        {
            if (document.ProtectedKey is null)
            {
                continue;
            }

            if (Key(document) is { } key)
            {
                hashes.Add(KeyedHashOf(key, address));
            }
            else
            {
                unknown = true;
            }
        }

        return (hashes, unknown);
    }

    private async Task<IReadOnlyList<SurveyInvitationOptOut>> RecordsAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
    {
        var values = hashes.Where(hash => !string.IsNullOrEmpty(hash)).Distinct(StringComparer.Ordinal).ToArray();

        if (values.Length == 0)
        {
            return [];
        }

        return (await _session.Query<SurveyInvitationOptOut, SurveyInvitationOptOutIndex>(index => index.AddressHash.IsIn(values), collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken)).ToList();
    }

    private byte[] Key(SurveyInvitationOptOutDocument document)
    {
        if (_keys.TryGetValue(document.ProtectedKey, out var cached))
        {
            return cached;
        }

        byte[] key;

        try
        {
            key = Convert.FromBase64String(_protector.Unprotect(document.ProtectedKey));
        }
        catch (CryptographicException exception)
        {
            _logger.LogError(exception, "The key of the survey invitation opt-out list could not be read; e-mail invitations are held back, without changing them, until the Data Protection keys are restored.");

            key = null;
        }

        _keys[document.ProtectedKey] = key;

        return key;
    }

    // The key documents: one, or two when two first unsubscribes raced. They hold the keys, and the hashes of the old
    // single list until the move step empties them, so they stay small.
    private async Task<List<SurveyInvitationOptOutDocument>> LoadAsync(CancellationToken cancellationToken)
        => _documents ??= [.. (await _session.Query<SurveyInvitationOptOutDocument>().ListAsync(cancellationToken)).OrderBy(document => document.Id)];
}
