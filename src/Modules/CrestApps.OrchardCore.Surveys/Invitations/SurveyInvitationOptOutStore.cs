using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Surveys.Core.Access;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// Reads and changes the addresses that unsubscribed from survey invitations. Addresses are kept as HMACs keyed by a
/// site key protected with ASP.NET Data Protection; plain SHA-256 hashes stored before the key existed are still
/// honored. The document is saved with a concurrency check, so two unsubscribes at once never lose one of them: the
/// later commit fails instead of overwriting the list (the unsubscribe page retries it). Two first-ever unsubscribes
/// at the same moment can each insert a list; every list is read, changes go to the oldest, and an opt-in removes the
/// address from all of them, so neither unsubscribe is lost. When a list's key can't be read (missing Data Protection
/// keys), an address it might hold is <see cref="SurveyInvitationOptOutState.Unknown"/>: nothing is sent to it, but
/// nothing is recorded as an opt-out either.
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

        var legacy = HashOf(address);
        var unknown = false;

        foreach (var document in await LoadAsync(cancellationToken))
        {
            if (document.AddressHashes.Contains(legacy))
            {
                return SurveyInvitationOptOutState.OptedOut;
            }

            if (document.ProtectedKey is null)
            {
                continue;
            }

            if (Key(document) is not { } key)
            {
                unknown = true;

                continue;
            }

            if (document.AddressHashes.Contains(KeyedHashOf(key, address)))
            {
                return SurveyInvitationOptOutState.OptedOut;
            }
        }

        return unknown ? SurveyInvitationOptOutState.Unknown : SurveyInvitationOptOutState.NotOptedOut;
    }

    /// <summary>
    /// Adds or removes an address. The change is saved with a concurrency check when the session commits.
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
        var changed = new HashSet<SurveyInvitationOptOutDocument>();

        if (documents.Count == 0)
        {
            documents.Add(new SurveyInvitationOptOutDocument());
        }

        // Changes go to the oldest list; another list exists only when two first unsubscribes raced.
        var primary = documents[0];

        if (primary.ProtectedKey is null)
        {
            primary.ProtectedKey = _protector.Protect(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            changed.Add(primary);
        }

        if (optedOut)
        {
            var key = Key(primary);

            // Without the key the address is kept under its plain hash, so the opt-out is still honored.
            if (key is null ? primary.AddressHashes.Add(legacy) : primary.AddressHashes.Add(KeyedHashOf(key, address)) | primary.AddressHashes.Remove(legacy))
            {
                changed.Add(primary);
            }
        }
        else
        {
            foreach (var document in documents)
            {
                var removed = document.AddressHashes.Remove(legacy);

                if (document.ProtectedKey is not null && Key(document) is { } key)
                {
                    removed |= document.AddressHashes.Remove(KeyedHashOf(key, address));
                }

                if (removed)
                {
                    changed.Add(document);
                }
            }
        }

        foreach (var document in changed)
        {
            await _session.SaveAsync(document, checkConcurrency: true, cancellationToken: cancellationToken);
        }
    }

    private static string Normalize(string address)
        => address.Trim().ToLowerInvariant();

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

    private async Task<List<SurveyInvitationOptOutDocument>> LoadAsync(CancellationToken cancellationToken)
        => _documents ??= [.. (await _session.Query<SurveyInvitationOptOutDocument>().ListAsync(cancellationToken)).OrderBy(document => document.Id)];
}
